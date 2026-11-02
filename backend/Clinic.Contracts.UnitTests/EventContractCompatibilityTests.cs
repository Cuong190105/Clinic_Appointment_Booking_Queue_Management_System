using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;
using Xunit;

namespace Clinic.Contracts.UnitTests;

/// <summary>
/// Phase 7: Event Contract compatibility, schema versioning, and tolerant reader tests.
/// Verifies that all domain event payloads round-trip cleanly and that forward/backward compatibility
/// rules are enforced (e.g. unknown JSON fields do not crash consumers).
/// </summary>
public sealed class EventContractCompatibilityTests
{
    [Fact]
    public void TolerantReader_IgnoresUnknownFields_WithoutBreakingDeserialization()
    {
        // Simulate an upstream service adding a new field (v1.1) consumed by a v1.0 consumer
        var jsonWithExtraFields = """
        {
            "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "eventType": "AppointmentConfirmed",
            "schemaVersion": 1,
            "occurredAt": "2026-11-02T10:00:00+00:00",
            "correlationId": "4ba85f64-5717-4562-b3fc-2c963f66afa7",
            "producer": "appointment-service",
            "newFutureProtocolField": "some-unrecognized-metadata",
            "payload": {
                "appointmentId": "5ca85f64-5717-4562-b3fc-2c963f66afa8",
                "patientId": "6da85f64-5717-4562-b3fc-2c963f66afa9",
                "doctorId": "7ea85f64-5717-4562-b3fc-2c963f66afb0",
                "confirmedAt": "2026-11-02T10:00:00+00:00",
                "extraFutureBusinessData": 12345
            }
        }
        """;

        var envelope = JsonSerializer.Deserialize<EventEnvelope<AppointmentConfirmed>>(
            jsonWithExtraFields,
            EventJsonSerializerOptions.Default);

        Assert.NotNull(envelope);
        Assert.Equal(Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"), envelope.EventId);
        Assert.Equal(EventTypes.AppointmentConfirmed, envelope.EventType);
        Assert.Equal(1, envelope.SchemaVersion);
        Assert.Equal("appointment-service", envelope.Producer);
        Assert.NotNull(envelope.Payload);
        Assert.Equal(Guid.Parse("5ca85f64-5717-4562-b3fc-2c963f66afa8"), envelope.Payload.AppointmentId);
    }

    [Fact]
    public void AllStandardEventTypes_HaveNonNullConstantNames()
    {
        var fields = typeof(EventTypes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.NotEmpty(fields);

        foreach (var field in fields)
        {
            var value = field.GetValue(null) as string;
            Assert.False(string.IsNullOrWhiteSpace(value), $"Event type {field.Name} should not be null or empty.");
        }
    }

    [Fact]
    public void QueueEvents_SerializationRoundTrip_PreservesAllContextProperties()
    {
        var ticketId = Guid.NewGuid();
        var apptId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Test PatientCalled with rich context
        var called = new PatientCalled(
            ticketId,
            apptId,
            doctorId,
            12,
            now,
            patientId,
            Guid.NewGuid(),
            "Nguyen Van An",
            "0901234567");

        var calledEnv = new EventEnvelope<PatientCalled>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.PatientCalled,
            SchemaVersion = 1,
            OccurredAt = now,
            CorrelationId = Guid.NewGuid(),
            Producer = "queue-service",
            Payload = called
        };

        var json = JsonSerializer.Serialize(calledEnv, EventJsonSerializerOptions.Default);
        var restored = JsonSerializer.Deserialize<EventEnvelope<PatientCalled>>(json, EventJsonSerializerOptions.Default);

        Assert.NotNull(restored);
        Assert.Equal(ticketId, restored.Payload.QueueTicketId);
        Assert.Equal(apptId, restored.Payload.AppointmentId);
        Assert.Equal(doctorId, restored.Payload.DoctorId);
        Assert.Equal(12, restored.Payload.QueueNumber);
        Assert.Equal(patientId, restored.Payload.PatientId);
        Assert.Equal("Nguyen Van An", restored.Payload.PatientName);
        Assert.Equal("0901234567", restored.Payload.PatientPhone);
    }

    [Fact]
    public void NotificationRequested_SerializationRoundTrip()
    {
        var recipientId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var notif = new NotificationRequested(
            recipientId,
            "appointment-reminder",
            "Appointment Reminder",
            "Your appointment is tomorrow at 9 AM.",
            new[] { "email", "in_app" },
            "patient@test.com",
            "0901234567",
            now.AddDays(1));

        var envelope = new EventEnvelope<NotificationRequested>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.NotificationRequested,
            SchemaVersion = 1,
            OccurredAt = now,
            CorrelationId = Guid.NewGuid(),
            Producer = "notification-service",
            Payload = notif
        };

        var json = JsonSerializer.Serialize(envelope, EventJsonSerializerOptions.Default);
        var restored = JsonSerializer.Deserialize<EventEnvelope<NotificationRequested>>(json, EventJsonSerializerOptions.Default);

        Assert.NotNull(restored);
        Assert.Equal(recipientId, restored.Payload.RecipientId);
        Assert.Equal("appointment-reminder", restored.Payload.TemplateKey);
        Assert.Equal("Appointment Reminder", restored.Payload.Title);
        Assert.Equal(2, restored.Payload.Channels.Length);
        Assert.Contains("email", restored.Payload.Channels);
        Assert.Contains("in_app", restored.Payload.Channels);
        Assert.Equal("patient@test.com", restored.Payload.Email);
    }
}
