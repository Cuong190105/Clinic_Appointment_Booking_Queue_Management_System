using Clinic.Contracts.Events;
using Clinic.MedicalRecords.Service.Entities;
using Clinic.MedicalRecords.Service.Persistence;
using Clinic.MedicalRecords.Service.Services;
using Microsoft.EntityFrameworkCore;

namespace Clinic.MedicalRecords.Service.Tests;

public sealed class MedicalRecordWorkflowTests
{
    private static MedicalRecordsDbContext Db() => new(new DbContextOptionsBuilder<MedicalRecordsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static EventEnvelope<T> Envelope<T>(Guid id, string type, T payload) => new() { EventId = id, EventType = type, SchemaVersion = 1, OccurredAt = DateTimeOffset.UtcNow, CorrelationId = Guid.NewGuid(), Producer = "test", Payload = payload };

    [Fact]
    public async Task ExamStarted_CreatesOneDraft_WhenDuplicated()
    {
        await using var db = Db(); var workflow = new MedicalRecordWorkflow(db); var eventId = Guid.NewGuid(); var appointment = Guid.NewGuid();
        var envelope = Envelope(eventId, EventTypes.ExamStarted, new ExamStarted(Guid.NewGuid(), appointment, Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), "Patient", "Doctor"));
        Assert.True(await workflow.HandleStartedAsync(envelope, default)); Assert.False(await workflow.HandleStartedAsync(envelope, default));
        var record = Assert.Single(await db.MedicalRecords.ToListAsync()); Assert.Equal(MedicalRecordStatus.Draft, record.Status); Assert.Equal("Patient", record.PatientName);
    }

    [Fact]
    public async Task CompletedBeforeStarted_NeverRegressesFromFinalized()
    {
        await using var db = Db(); var workflow = new MedicalRecordWorkflow(db); var appointment = Guid.NewGuid(); var ticket = Guid.NewGuid(); var doctor = Guid.NewGuid(); var patient = Guid.NewGuid();
        await workflow.HandleCompletedAsync(Envelope(Guid.NewGuid(), EventTypes.ExamCompleted, new ExamCompleted(ticket, appointment, doctor, DateTimeOffset.UtcNow, patient)), default);
        await workflow.HandleStartedAsync(Envelope(Guid.NewGuid(), EventTypes.ExamStarted, new ExamStarted(ticket, appointment, doctor, DateTimeOffset.UtcNow.AddMinutes(-5), patient, "Patient", "Doctor")), default);
        var record = Assert.Single(await db.MedicalRecords.ToListAsync()); Assert.Equal(MedicalRecordStatus.Finalized, record.Status); Assert.Equal(2, record.WorkflowVersion);
        Assert.Equal("Patient", record.PatientName); Assert.Equal("Doctor", record.DoctorName);
        var history = Assert.Single(await db.MedicalHistory.ToListAsync()); Assert.Equal(record.Id, history.MedicalRecordId); Assert.Equal(2, history.SourceVersion);
    }

    [Fact]
    public async Task HistoryProjection_CanBeRebuiltWithoutChangingClinicalRecord()
    {
        await using var db = Db();
        var record = new MedicalRecord { AppointmentId = Guid.NewGuid(), PatientId = Guid.NewGuid(), DoctorId = Guid.NewGuid(), DoctorName = "Doctor",
            ExamDate = new DateOnly(2026, 11, 2), Status = MedicalRecordStatus.Finalized, WorkflowVersion = 2, Diagnosis = "D" };
        db.MedicalRecords.Add(record); await db.SaveChangesAsync();
        var originalUpdatedAt = record.UpdatedAt;

        Assert.Equal(1, await MedicalHistoryProjector.RebuildAsync(db, default));
        await db.SaveChangesAsync();

        var history = Assert.Single(await db.MedicalHistory.ToListAsync());
        Assert.Equal(record.PatientId, history.PatientId); Assert.Equal("D", history.Diagnosis);
        Assert.Equal(originalUpdatedAt, record.UpdatedAt);
    }
}
