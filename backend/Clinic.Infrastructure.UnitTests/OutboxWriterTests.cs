using System.Text.Json;
using System.Diagnostics;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;
using Clinic.Infrastructure.Sqlserver.Outbox;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.UnitTests;

public sealed class OutboxWriterTests
{
    [Fact]
    public async Task Add_creates_pending_message_with_serialized_envelope()
    {
        await using var db = CreateDbContext();
        var writer = new OutboxWriter(db);
        var eventId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        using var requestActivity = new Activity("http.request")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        writer.Add(
            new EventEnvelope<AppointmentCheckedIn>
            {
                EventId = eventId,
                EventType = EventTypes.AppointmentCheckedIn,
                SchemaVersion = 1,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = correlationId,
                Producer = "appointment-service",
                Payload = new AppointmentCheckedIn(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow)
            },
            "appointment.checked-in");

        await db.SaveChangesAsync();
        var message = await db.OutboxMessages.SingleAsync();

        Assert.Equal(eventId, message.EventId);
        Assert.Equal(correlationId, message.CorrelationId);
        Assert.Equal(requestActivity.Id, message.TraceParent);
        Assert.Equal(EventTypes.AppointmentCheckedIn, message.EventType);
        Assert.Equal("appointment.checked-in", message.RoutingKey);
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(0, message.RetryCount);
        Assert.Null(message.PublishedAt);

        var envelope = JsonSerializer.Deserialize<
            EventEnvelope<AppointmentCheckedIn>>(
            message.Payload,
            EventJsonSerializerOptions.Default);

        Assert.NotNull(envelope);
        Assert.Equal(eventId, envelope.EventId);
        Assert.Equal(EventTypes.AppointmentCheckedIn, envelope.EventType);
    }

    [Fact]
    public async Task Add_rejects_empty_routing_key()
    {
        await using var db = CreateDbContext();
        var writer = new OutboxWriter(db);
        var envelope = new EventEnvelope<AppointmentCheckedIn>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.AppointmentCheckedIn,
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Producer = "appointment-service",
            Payload = new AppointmentCheckedIn(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTimeOffset.UtcNow)
        };

        Assert.Throws<ArgumentException>(() => writer.Add(envelope, " "));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
