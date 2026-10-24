using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;

namespace Clinic.Contracts.UnitTests;

public sealed class EventEnvelopeTests
{
    [Fact]
    public void Serializes_using_the_public_contract_shape()
    {
        var eventId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.Parse("2026-10-18T10:00:00+00:00");

        var envelope = new EventEnvelope<AppointmentCheckedIn>
        {
            EventId = eventId,
            EventType = EventTypes.AppointmentCheckedIn,
            SchemaVersion = 1,
            OccurredAt = occurredAt,
            CorrelationId = correlationId,
            CausationId = causationId,
            Producer = "appointment-service",
            Payload = new AppointmentCheckedIn(
                appointmentId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                occurredAt)
        };

        var json = JsonSerializer.Serialize(envelope, EventJsonSerializerOptions.Default);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(eventId.ToString(), document.RootElement.GetProperty("eventId").GetString());
        Assert.Equal(EventTypes.AppointmentCheckedIn, document.RootElement.GetProperty("eventType").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("appointment-service", document.RootElement.GetProperty("producer").GetString());
        Assert.Equal(appointmentId.ToString(), document.RootElement.GetProperty("payload").GetProperty("appointmentId").GetString());
        Assert.False(document.RootElement.GetProperty("payload").TryGetProperty("visitId", out _));
    }

    [Fact]
    public void Round_trips_an_envelope_without_losing_metadata()
    {
        var envelope = new EventEnvelope<AppointmentCheckedIn>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.AppointmentCheckedIn,
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            CausationId = null,
            Producer = "appointment-service",
            Payload = new AppointmentCheckedIn(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTimeOffset.UtcNow)
        };

        var json = JsonSerializer.Serialize(envelope, EventJsonSerializerOptions.Default);
        var result = JsonSerializer.Deserialize<EventEnvelope<AppointmentCheckedIn>>(
            json,
            EventJsonSerializerOptions.Default);

        Assert.NotNull(result);
        Assert.Equal(envelope.EventId, result.EventId);
        Assert.Equal(envelope.CorrelationId, result.CorrelationId);
        Assert.Null(result.CausationId);
        Assert.Equal(envelope.Payload, result.Payload);
    }

    [Fact]
    public void Exam_completed_uses_appointment_identity_without_visit_id()
    {
        var examCompleted = new ExamCompleted(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(examCompleted, EventJsonSerializerOptions.Default);
        using var document = JsonDocument.Parse(json);

        Assert.False(document.RootElement.TryGetProperty("visitId", out _));
        Assert.True(document.RootElement.TryGetProperty("appointmentId", out _));
    }
}
