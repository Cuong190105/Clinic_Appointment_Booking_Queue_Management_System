using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;

namespace Clinic.Contracts.UnitTests;

public sealed class MedicalRecordsEventContractTests
{
    [Fact]
    public void ExamStarted_Serializes_With_AppointmentId_And_ContextSnapshot_Without_PHI()
    {
        var queueTicketId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var doctorName = "Dr. John Doe";
        var queueDate = new DateOnly(2026, 10, 26);
        var startedAt = DateTimeOffset.Parse("2026-10-26T08:30:00+00:00");

        var examStarted = new ExamStarted(
            queueTicketId,
            appointmentId,
            patientId,
            doctorId,
            doctorName,
            queueDate,
            startedAt);

        var envelope = new EventEnvelope<ExamStarted>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.ExamStarted,
            SchemaVersion = 1,
            OccurredAt = startedAt,
            CorrelationId = Guid.NewGuid(),
            Producer = "queue-service",
            Payload = examStarted
        };

        var json = JsonSerializer.Serialize(envelope, EventJsonSerializerOptions.Default);
        using var doc = JsonDocument.Parse(json);
        var payload = doc.RootElement.GetProperty("payload");

        // Verify key identity & context snapshot
        Assert.Equal(appointmentId.ToString(), payload.GetProperty("appointmentId").GetString());
        Assert.Equal(queueTicketId.ToString(), payload.GetProperty("queueTicketId").GetString());
        Assert.Equal(patientId.ToString(), payload.GetProperty("patientId").GetString());
        Assert.Equal(doctorId.ToString(), payload.GetProperty("doctorId").GetString());
        Assert.Equal(doctorName, payload.GetProperty("doctorName").GetString());
        Assert.Equal("2026-10-26", payload.GetProperty("queueDate").GetString());
        Assert.Equal(startedAt, payload.GetProperty("startedAt").GetDateTimeOffset());

        // Verify PHI is NOT present
        Assert.False(payload.TryGetProperty("diagnosis", out _));
        Assert.False(payload.TryGetProperty("symptoms", out _));
        Assert.False(payload.TryGetProperty("prescription", out _));
        Assert.False(payload.TryGetProperty("notes", out _));
        Assert.False(payload.TryGetProperty("visitId", out _));
    }

    [Fact]
    public void ExamStarted_Backward_Compatible_Constructor_Succeeds()
    {
        var queueTicketId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        var legacy = new ExamStarted(queueTicketId, appointmentId, doctorId, startedAt);

        Assert.Equal(queueTicketId, legacy.QueueTicketId);
        Assert.Equal(appointmentId, legacy.AppointmentId);
        Assert.Equal(doctorId, legacy.DoctorId);
        Assert.Equal(Guid.Empty, legacy.PatientId);
        Assert.Null(legacy.DoctorName);
        Assert.Null(legacy.QueueDate);
        Assert.Equal(startedAt, legacy.StartedAt);
    }

    [Fact]
    public void ExamCompleted_Keyed_By_AppointmentId_Without_PHI()
    {
        var queueTicketId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var completedAt = DateTimeOffset.Parse("2026-10-26T09:00:00+00:00");

        var examCompleted = new ExamCompleted(
            queueTicketId,
            appointmentId,
            doctorId,
            completedAt);

        var envelope = new EventEnvelope<ExamCompleted>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.ExamCompleted,
            SchemaVersion = 1,
            OccurredAt = completedAt,
            CorrelationId = Guid.NewGuid(),
            Producer = "queue-service",
            Payload = examCompleted
        };

        var json = JsonSerializer.Serialize(envelope, EventJsonSerializerOptions.Default);
        using var doc = JsonDocument.Parse(json);
        var payload = doc.RootElement.GetProperty("payload");

        Assert.Equal(appointmentId.ToString(), payload.GetProperty("appointmentId").GetString());
        Assert.Equal(queueTicketId.ToString(), payload.GetProperty("queueTicketId").GetString());
        Assert.Equal(doctorId.ToString(), payload.GetProperty("doctorId").GetString());
        Assert.Equal(completedAt, payload.GetProperty("completedAt").GetDateTimeOffset());

        // Verify PHI is NOT present
        Assert.False(payload.TryGetProperty("diagnosis", out _));
        Assert.False(payload.TryGetProperty("symptoms", out _));
        Assert.False(payload.TryGetProperty("prescription", out _));
        Assert.False(payload.TryGetProperty("isFinalize", out _));
    }
}
