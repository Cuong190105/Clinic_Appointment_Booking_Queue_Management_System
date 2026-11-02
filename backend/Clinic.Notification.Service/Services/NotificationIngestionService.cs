using Clinic.Contracts.Events;
using Clinic.Notification.Service.Entities;
using Clinic.Notification.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Clinic.Notification.Service.Services;

public sealed class NotificationIngestionService(NotificationDbContext db)
{
    public async Task<bool> PersistAsync(Guid eventId, Guid correlationId, NotificationRequested request, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (await db.Inbox.AnyAsync(x => x.EventId == eventId, ct)) return false;
        var message = new NotificationMessage
        {
            EventId = eventId,
            RecipientId = request.RecipientId,
            TemplateKey = request.TemplateKey,
            Title = request.Title,
            Body = request.Body,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            CorrelationId = correlationId
        };
        foreach (var name in request.Channels.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            message.Deliveries.Add(new NotificationDelivery
            {
                Channel = name.ToLowerInvariant(),
                Status = DeliveryStatus.Pending,
                NextAttemptAt = request.ScheduledAt ?? DateTimeOffset.UtcNow
            });
        if (message.Deliveries.Count == 0) throw new InvalidOperationException("A notification requires at least one delivery channel.");
        db.Messages.Add(message);
        db.Inbox.Add(new InboxMessage { EventId = eventId, ProcessedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return true;
    }
}
