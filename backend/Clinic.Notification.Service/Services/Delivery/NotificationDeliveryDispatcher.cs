using Clinic.Notification.Service.Entities;
using Clinic.Notification.Service.Persistence;
using Clinic.Notification.Service.Services;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Notification.Service.Services.Delivery;

public interface INotificationDeliveryDispatcher
{
    TimeSpan TimeoutBudget { get; set; }
    Task<DeliveryStatus> ProcessDeliveryAsync(Guid deliveryId, CancellationToken ct = default);
    Task<int> ProcessDueDeliveriesAsync(int batchSize = 20, CancellationToken ct = default);
}

public sealed class NotificationDeliveryDispatcher : INotificationDeliveryDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEmailSender _emailSender;
    private readonly ISmsSender _smsSender;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly ILogger<NotificationDeliveryDispatcher> _logger;

    public TimeSpan TimeoutBudget { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Password-reset secrets are discarded if not delivered within this window.</summary>
    public static readonly TimeSpan PasswordResetRetention = TimeSpan.FromMinutes(30);

    public NotificationDeliveryDispatcher(
        IServiceProvider serviceProvider,
        IEmailSender emailSender,
        ISmsSender smsSender,
        IRealtimeNotifier realtimeNotifier,
        ILogger<NotificationDeliveryDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _emailSender = emailSender;
        _smsSender = smsSender;
        _realtimeNotifier = realtimeNotifier;
        _logger = logger;
    }

    public async Task<int> ProcessDueDeliveriesAsync(int batchSize = 20, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        var now = DateTimeOffset.UtcNow;
        var dueDeliveryIds = await db.DeliveryOutboxMessages
            .Where(m => m.Status == DeliveryStatus.Pending
                        && (m.ScheduledFor == null || m.ScheduledFor <= now)
                        && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .Select(m => m.Id)
            .ToListAsync(ct);

        var processedCount = 0;
        foreach (var id in dueDeliveryIds)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await ProcessDeliveryAsync(id, ct);
                processedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process due delivery ID={DeliveryId}", id);
            }
        }

        return processedCount;
    }

    public async Task<DeliveryStatus> ProcessDeliveryAsync(Guid deliveryId, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        var delivery = await db.DeliveryOutboxMessages.FindAsync([deliveryId], cancellationToken: ct);
        if (delivery == null || delivery.Status != DeliveryStatus.Pending)
        {
            return delivery?.Status ?? DeliveryStatus.Failed;
        }

        var protector = scope.ServiceProvider.GetService<ISecretProtector>();

        // Durable suppression check right before dispatch: cancelled/rescheduled appointments never get reminders.
        if (delivery.NotificationType == NotificationType.AppointmentReminder && delivery.AppointmentId.HasValue)
        {
            var slotKey = delivery.SlotKey ?? NotificationPolicyEngine.AnySlot;
            var suppressed = await db.ReminderSuppressions.AnyAsync(s =>
                s.AppointmentId == delivery.AppointmentId.Value &&
                (s.SlotKey == slotKey || s.SlotKey == NotificationPolicyEngine.AnySlot), ct);
            if (suppressed)
            {
                delivery.Status = DeliveryStatus.Suppressed;
                if (delivery.NotificationId.HasValue)
                {
                    var item = await db.Notifications.FindAsync([delivery.NotificationId.Value], cancellationToken: ct);
                    if (item != null) item.SuppressedAt = DateTimeOffset.UtcNow;
                }
                await db.SaveChangesAsync(ct);
                return delivery.Status;
            }
        }

        // Secrets: expire stale password-reset deliveries and never send an unrecoverable payload.
        var content = delivery.Content;
        if (delivery.NotificationType == NotificationType.PasswordReset)
        {
            if (DateTimeOffset.UtcNow - delivery.CreatedAt > PasswordResetRetention)
            {
                delivery.Status = DeliveryStatus.DeadLettered;
                delivery.Content = DataProtectionSecretProtector.Redacted;
                db.DeliveryAttempts.Add(NewAttempt(delivery, delivery.AttemptsCount, DeliveryStatus.Failed, "Expired before delivery; secret discarded."));
                await db.SaveChangesAsync(ct);
                return delivery.Status;
            }

            if (protector != null && protector.IsProtected(content))
            {
                var plain = protector.Unprotect(content);
                if (plain == null)
                {
                    delivery.Status = DeliveryStatus.DeadLettered;
                    delivery.Content = DataProtectionSecretProtector.Redacted;
                    db.DeliveryAttempts.Add(NewAttempt(delivery, delivery.AttemptsCount, DeliveryStatus.Failed, "Secret unavailable (key rotated or lost); discarded."));
                    await db.SaveChangesAsync(ct);
                    return delivery.Status;
                }
                content = plain;
            }
        }

        delivery.AttemptsCount++;
        ProviderDeliveryResult result;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeoutBudget); // Bounded timeout

            result = delivery.Channel switch
            {
                NotificationChannel.InApp => await DeliverInAppAsync(delivery, timeoutCts.Token),
                NotificationChannel.Email => await _emailSender.SendEmailAsync(delivery.RecipientAddress, delivery.Subject ?? "Thông báo", content, timeoutCts.Token),
                NotificationChannel.Sms => await _smsSender.SendSmsAsync(delivery.RecipientAddress, content, timeoutCts.Token),
                _ => new ProviderDeliveryResult(ProviderResultCategory.PermanentFailure, $"Unsupported channel {delivery.Channel}")
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            result = new ProviderDeliveryResult(ProviderResultCategory.Timeout, "Provider delivery timed out after 5s.");
        }
        catch (Exception ex)
        {
            if (delivery.NotificationType == NotificationType.PasswordReset)
            {
                // Provider exception text/stack traces may contain request content. Never log them for reset deliveries.
                _logger.LogError(
                    "Password-reset provider delivery failed for delivery ID={DeliveryId}; exception type={ExceptionType}",
                    delivery.Id,
                    ex.GetType().Name);
            }
            else
            {
                _logger.LogError(ex, "Provider delivery failed with exception for delivery ID={DeliveryId}", delivery.Id);
            }
            result = new ProviderDeliveryResult(ProviderResultCategory.TransientFailure, "Provider call failed.");
        }

        var attempt = NewAttempt(delivery, delivery.AttemptsCount, DeliveryStatus.Pending, result.ProviderResponse);
        attempt.ProviderReference = result.ProviderReference;

        if (result.Category == ProviderResultCategory.Success)
        {
            delivery.Status = DeliveryStatus.Delivered;
            delivery.DeliveredAt = DateTimeOffset.UtcNow;
            attempt.Status = DeliveryStatus.Delivered;
        }
        else if (result.Category == ProviderResultCategory.PermanentFailure)
        {
            delivery.Status = DeliveryStatus.DeadLettered;
            attempt.Status = DeliveryStatus.Failed;
        }
        else // TransientFailure, Timeout, Ambiguous
        {
            attempt.Status = DeliveryStatus.Failed;

            if (delivery.AttemptsCount >= delivery.MaxAttempts)
            {
                delivery.Status = DeliveryStatus.DeadLettered;
            }
            else
            {
                delivery.Status = DeliveryStatus.Pending;
                delivery.NextAttemptAt = DeliveryBackoffStrategy.CalculateNextAttemptAt(delivery.AttemptsCount, DateTimeOffset.UtcNow);
            }
        }

        // Secrets never outlive their delivery: redact once the message is terminal.
        if (delivery.NotificationType == NotificationType.PasswordReset && delivery.Status != DeliveryStatus.Pending)
        {
            delivery.Content = DataProtectionSecretProtector.Redacted;
        }

        db.DeliveryAttempts.Add(attempt);
        await db.SaveChangesAsync(ct);

        return delivery.Status;
    }

    private static DeliveryAttempt NewAttempt(DeliveryOutboxMessage delivery, int attemptNumber, DeliveryStatus status, string? response) => new()
    {
        Id = Guid.NewGuid(),
        DeliveryOutboxMessageId = delivery.Id,
        AttemptNumber = attemptNumber,
        AttemptedAt = DateTimeOffset.UtcNow,
        Status = status,
        ProviderResponse = Sanitize(response),
        CorrelationId = delivery.CorrelationId
    };

    private async Task<ProviderDeliveryResult> DeliverInAppAsync(DeliveryOutboxMessage delivery, CancellationToken ct)
    {
        if (delivery.NotificationId.HasValue)
        {
            await _realtimeNotifier.SendNotificationHintAsync(
                delivery.RecipientId,
                delivery.NotificationId.Value,
                delivery.NotificationType.ToString(),
                ct);
        }

        return new ProviderDeliveryResult(ProviderResultCategory.Success, "In-app notification hint dispatched.");
    }

    private static string Sanitize(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)) return "No provider response";
        // Ensure no reset codes or secrets are stored in attempt audit
        return response.Length > 1000 ? response[..1000] : response;
    }
}
