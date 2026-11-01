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

        // 2. Policy Evaluation (unwrap EventEnvelope<T> if the producer serialized the full envelope)
        var bodyElement = UnwrapPayload(payloadElement);
        IReadOnlyList<NotificationIntent> intents = Array.Empty<NotificationIntent>();
        Guid? appointmentId = null;
        Guid? queueTicketId = null;
        var now = DateTimeOffset.UtcNow;

        if (eventType == EventTypes.AppointmentConfirmed)
        {
            var payload = bodyElement.Deserialize<AppointmentConfirmed>(EventJsonSerializerOptions.Default);
            if (payload != null)
            {
                appointmentId = payload.AppointmentId;
                var all = new List<NotificationIntent>(_policyEngine.EvaluateAppointmentConfirmed(eventId, payload));

                // Reminder is skipped when the slot was already cancelled / rescheduled away
                // (late or re-ordered delivery must not resurrect it).
                var slotKey = NotificationPolicyEngine.BuildSlotKey(payload.WorkScheduleId, payload.TimeSlot);
                var tombstoned = await db.ReminderSuppressions.AnyAsync(s =>
                    s.AppointmentId == payload.AppointmentId &&
                    (s.SlotKey == slotKey || s.SlotKey == NotificationPolicyEngine.AnySlot), ct);
                if (!tombstoned)
                {
                    all.AddRange(_policyEngine.EvaluateAppointmentReminder(eventId, payload, now));
                }

                intents = all;
            }
        }
        else if (eventType == EventTypes.AppointmentCancelled)
        {
            var payload = bodyElement.Deserialize<AppointmentCancelled>(EventJsonSerializerOptions.Default);
            if (payload != null)
            {
                appointmentId = payload.AppointmentId;
                intents = _policyEngine.EvaluateAppointmentCancelled(eventId, payload);

                if (_policyEngine.ShouldCancelPendingReminder(payload))
                {
                    await SuppressRemindersAsync(db, eventId, payload, now, ct);
                }
            }
        }
        else if (eventType == EventTypes.PatientCalled)
        {
            var payload = bodyElement.Deserialize<PatientCalled>(EventJsonSerializerOptions.Default);
            if (payload != null)
            {
                appointmentId = payload.AppointmentId != Guid.Empty ? payload.AppointmentId : null;
                queueTicketId = payload.QueueTicketId != Guid.Empty ? payload.QueueTicketId : null;

                // Enrich linkage/contact snapshot from prior local notification for the same appointment if Queue only carried PatientId/AppointmentId.
                if ((payload.IdentityUserId == null || string.IsNullOrWhiteSpace(payload.PatientPhone)) && payload.AppointmentId != Guid.Empty)
                {
                    var prior = await db.Notifications.AsNoTracking()
                        .Where(n => n.AppointmentId == payload.AppointmentId)
                        .OrderByDescending(n => n.CreatedAt)
                        .FirstOrDefaultAsync(ct);

                    if (prior != null)
                    {
                        var resolvedIdentity = payload.IdentityUserId
                            ?? (prior.RecipientType == RecipientType.Account ? prior.RecipientId : null);
                        payload = payload with
                        {
                            IdentityUserId = resolvedIdentity,
                            PatientName = string.IsNullOrWhiteSpace(payload.PatientName) ? prior.ContactName : payload.PatientName,
                            PatientPhone = string.IsNullOrWhiteSpace(payload.PatientPhone) ? prior.ContactPhone : payload.PatientPhone
                        };
                    }
                    else
                    {
                        var priorSms = await db.DeliveryOutboxMessages.AsNoTracking()
                            .Where(o => o.AppointmentId == payload.AppointmentId && o.Channel == NotificationChannel.Sms)
                            .OrderByDescending(o => o.CreatedAt)
                            .FirstOrDefaultAsync(ct);
                        if (priorSms != null && string.IsNullOrWhiteSpace(payload.PatientPhone))
                        {
                            payload = payload with { PatientPhone = priorSms.RecipientAddress };
                        }
                    }
                }

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
                    AppointmentId = intent.AppointmentId ?? appointmentId,
                    QueueTicketId = queueTicketId,
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
                NextAttemptAt = intent.ScheduledFor ?? now,
                AppointmentId = intent.AppointmentId ?? appointmentId,
                SlotKey = intent.SlotKey,
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

    private static JsonElement UnwrapPayload(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("payload", out var camel) && camel.ValueKind == JsonValueKind.Object)
                return camel;
            if (element.TryGetProperty("Payload", out var pascal) && pascal.ValueKind == JsonValueKind.Object)
                return pascal;
        }
        return element;
    }

    private static async Task SuppressRemindersAsync(
        NotificationDbContext db,
        Guid eventId,
        AppointmentCancelled payload,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var slotKey = NotificationPolicyEngine.BuildSlotKey(payload.WorkScheduleId, payload.TimeSlot);

        if (!await db.ReminderSuppressions.AnyAsync(s => s.AppointmentId == payload.AppointmentId && s.SlotKey == slotKey, ct))
        {
            db.ReminderSuppressions.Add(new ReminderSuppression
            {
                AppointmentId = payload.AppointmentId,
                SlotKey = slotKey,
                EventId = eventId,
                SuppressedAt = now
            });
        }

        var pending = await db.DeliveryOutboxMessages
            .Where(o => o.AppointmentId == payload.AppointmentId
                        && o.NotificationType == NotificationType.AppointmentReminder
                        && o.Status == DeliveryStatus.Pending
                        && (slotKey == NotificationPolicyEngine.AnySlot || o.SlotKey == slotKey))
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        var notificationIds = pending.Where(p => p.NotificationId.HasValue).Select(p => p.NotificationId!.Value).ToList();
        var notifications = await db.Notifications.Where(n => notificationIds.Contains(n.Id)).ToListAsync(ct);

        foreach (var delivery in pending)
        {
            delivery.Status = DeliveryStatus.Suppressed;
        }
        foreach (var notification in notifications)
        {
            notification.SuppressedAt = now;
        }
    }
}
