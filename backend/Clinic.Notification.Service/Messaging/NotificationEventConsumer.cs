using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;
using Clinic.Notification.Service.Entities;
using Clinic.Notification.Service.Persistence;
using Clinic.Notification.Service.Services;
using Clinic.Notification.Service.Services.Delivery;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Clinic.Notification.Service.Messaging;

public interface INotificationEventProcessor
{
    Task<bool> ProcessEventAsync(Guid eventId, string eventType, JsonElement payloadElement, Guid? correlationId, CancellationToken ct = default);
}

public sealed class NotificationEventProcessor : INotificationEventProcessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationPolicyEngine _policyEngine;
    private readonly ILogger<NotificationEventProcessor> _logger;

    public NotificationEventProcessor(
        IServiceProvider serviceProvider,
        INotificationPolicyEngine policyEngine,
        ILogger<NotificationEventProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _policyEngine = policyEngine;
        _logger = logger;
    }

    public async Task<bool> ProcessEventAsync(Guid eventId, string eventType, JsonElement payloadElement, Guid? correlationId, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        // 1. Inbox Deduplication
        if (await db.InboxMessages.AnyAsync(m => m.Id == eventId, ct))
        {
            _logger.LogInformation("Event ID={EventId} already processed in inbox. Skipping duplicate.", eventId);
            return true;
        }

        var inbox = new InboxMessage
        {
            Id = eventId,
            EventType = eventType,
            ReceivedAt = DateTimeOffset.UtcNow,
            Status = "Processing"
        };
        db.InboxMessages.Add(inbox);

        // 2. Policy Evaluation
        IReadOnlyList<NotificationIntent> intents = Array.Empty<NotificationIntent>();

        if (eventType == EventTypes.AppointmentConfirmed)
        {
            var payload = payloadElement.Deserialize<AppointmentConfirmed>(EventJsonSerializerOptions.Default);
            if (payload != null)
            {
                intents = _policyEngine.EvaluateAppointmentConfirmed(eventId, payload);
            }
        }
        else if (eventType == EventTypes.AppointmentCancelled)
        {
            var payload = payloadElement.Deserialize<AppointmentCancelled>(EventJsonSerializerOptions.Default);
            if (payload != null)
            {
                intents = _policyEngine.EvaluateAppointmentCancelled(eventId, payload);
            }
        }
        else if (eventType == EventTypes.PatientCalled)
        {
            var payload = payloadElement.Deserialize<PatientCalled>(EventJsonSerializerOptions.Default);
            if (payload != null)
            {
                intents = _policyEngine.EvaluatePatientCalled(eventId, payload);
            }
        }

        // 3. Persist Notifications and Outbox atomically
        foreach (var intent in intents)
        {
            // Avoid duplicate outbox message if already exists
            if (await db.DeliveryOutboxMessages.AnyAsync(o => o.IdempotencyKey == intent.IdempotencyKey, ct))
            {
                continue;
            }

            NotificationItem? notification = null;
            if (intent.Channel == NotificationChannel.InApp)
            {
                notification = new NotificationItem
                {
                    Id = Guid.NewGuid(),
                    RecipientId = intent.RecipientId,
                    RecipientType = intent.RecipientType,
                    NotificationType = intent.NotificationType,
                    Title = intent.Title,
                    Body = intent.Body,
                    IsRead = false,
                    ContactName = intent.ContactSnapshot?.FullName,
                    ContactEmail = intent.ContactSnapshot?.Email,
                    ContactPhone = intent.ContactSnapshot?.PhoneNumber,
                    CorrelationId = correlationId,
                    ScheduledFor = intent.ScheduledFor
                };
                db.Notifications.Add(notification);
            }

            var address = intent.Channel switch
            {
                NotificationChannel.InApp => "in-app",
                NotificationChannel.Email => intent.ContactSnapshot?.Email ?? string.Empty,
                NotificationChannel.Sms => intent.ContactSnapshot?.PhoneNumber ?? string.Empty,
                _ => string.Empty
            };

            var outbox = new DeliveryOutboxMessage
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                Notification = notification,
                RecipientId = intent.RecipientId,
                RecipientType = intent.RecipientType,
                Channel = intent.Channel,
                NotificationType = intent.NotificationType,
                IdempotencyKey = intent.IdempotencyKey,
                RecipientAddress = address,
                Subject = intent.Title,
                Content = intent.Body,
                Status = DeliveryStatus.Pending,
                ScheduledFor = intent.ScheduledFor,
                CorrelationId = correlationId
            };
            db.DeliveryOutboxMessages.Add(outbox);
        }

        inbox.Status = "Processed";
        inbox.ProcessedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Successfully processed event ID={EventId}, generated {Count} outbox deliveries.", eventId, intents.Count);
        return true;
    }
}

