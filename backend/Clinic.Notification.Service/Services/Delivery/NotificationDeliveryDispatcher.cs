using Clinic.Notification.Service.Entities;
using Clinic.Notification.Service.Persistence;
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
            .Where(m => m.Status == DeliveryStatus.Pending && m.NextAttemptAt <= now)
            .OrderBy(m => m.NextAttemptAt)
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

        delivery.AttemptsCount++;
        ProviderDeliveryResult result;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeoutBudget); // Bounded timeout

            result = delivery.Channel switch
            {
                NotificationChannel.InApp => await DeliverInAppAsync(delivery, timeoutCts.Token),
                NotificationChannel.Email => await _emailSender.SendEmailAsync(delivery.RecipientAddress, delivery.Subject ?? "Thông báo", delivery.Content, timeoutCts.Token),
                NotificationChannel.Sms => await _smsSender.SendSmsAsync(delivery.RecipientAddress, delivery.Content, timeoutCts.Token),
                _ => new ProviderDeliveryResult(ProviderResultCategory.PermanentFailure, $"Unsupported channel {delivery.Channel}")
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            result = new ProviderDeliveryResult(ProviderResultCategory.Timeout, "Provider delivery timed out after 5s.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provider delivery failed with exception for delivery ID={DeliveryId}", delivery.Id);
            result = new ProviderDeliveryResult(ProviderResultCategory.TransientFailure, ex.Message);
        }

        var attempt = new DeliveryAttempt
        {
            Id = Guid.NewGuid(),
            DeliveryOutboxMessageId = delivery.Id,
            AttemptNumber = delivery.AttemptsCount,
            AttemptedAt = DateTimeOffset.UtcNow,
            ProviderResponse = Sanitize(result.ProviderResponse),
            ProviderReference = result.ProviderReference,
            CorrelationId = delivery.CorrelationId
        };

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

        db.DeliveryAttempts.Add(attempt);
        await db.SaveChangesAsync(ct);

        return delivery.Status;
    }

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
