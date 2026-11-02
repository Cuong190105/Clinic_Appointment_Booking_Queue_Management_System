using Clinic.Contracts.Events;
using Clinic.MedicalRecords.Service.Entities;
using Clinic.MedicalRecords.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Clinic.MedicalRecords.Service.Services;

public sealed class MedicalRecordWorkflow(MedicalRecordsDbContext db)
{
    public async Task<bool> HandleStartedAsync(EventEnvelope<ExamStarted> envelope, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (await db.InboxMessages.AnyAsync(x => x.EventId == envelope.EventId, ct)) return false;
        var record = await db.MedicalRecords.SingleOrDefaultAsync(x => x.AppointmentId == envelope.Payload.AppointmentId, ct);
        if (record is null)
        {
            record = new MedicalRecord
            {
                AppointmentId = envelope.Payload.AppointmentId,
                QueueTicketId = envelope.Payload.QueueTicketId,
                PatientId = envelope.Payload.PatientId,
                PatientName = envelope.Payload.PatientName ?? string.Empty,
                DoctorId = envelope.Payload.DoctorId,
                DoctorName = envelope.Payload.DoctorName ?? string.Empty,
                ExamDate = DateOnly.FromDateTime(envelope.Payload.StartedAt.UtcDateTime),
                ExamStartTime = envelope.Payload.StartedAt,
                Status = MedicalRecordStatus.Draft,
                WorkflowVersion = 1
            };
            db.MedicalRecords.Add(record);
        }
        else
        {
            // A late ExamStarted may enrich a record created by ExamCompleted, but it must
            // never regress Finalized back to Draft or lower its workflow version.
            if (record.ExamStartTime is null || envelope.Payload.StartedAt < record.ExamStartTime)
                record.ExamStartTime = envelope.Payload.StartedAt;
            if (record.PatientId == Guid.Empty) record.PatientId = envelope.Payload.PatientId;
            if (string.IsNullOrWhiteSpace(record.PatientName)) record.PatientName = envelope.Payload.PatientName ?? string.Empty;
            if (record.DoctorId == Guid.Empty) record.DoctorId = envelope.Payload.DoctorId;
            if (string.IsNullOrWhiteSpace(record.DoctorName)) record.DoctorName = envelope.Payload.DoctorName ?? string.Empty;
            record.WorkflowVersion = Math.Max(record.WorkflowVersion, 1);
            record.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await MedicalHistoryProjector.UpsertAsync(db, record, ct);
        db.InboxMessages.Add(new InboxMessage { EventId = envelope.EventId, EventType = envelope.EventType, ProcessedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> HandleCompletedAsync(EventEnvelope<ExamCompleted> envelope, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (await db.InboxMessages.AnyAsync(x => x.EventId == envelope.EventId, ct)) return false;
        var record = await db.MedicalRecords.SingleOrDefaultAsync(x => x.AppointmentId == envelope.Payload.AppointmentId, ct);
        if (record is null)
        {
            record = new MedicalRecord
            {
                AppointmentId = envelope.Payload.AppointmentId,
                QueueTicketId = envelope.Payload.QueueTicketId,
                PatientId = envelope.Payload.PatientId,
                DoctorId = envelope.Payload.DoctorId,
                ExamDate = DateOnly.FromDateTime(envelope.Payload.CompletedAt.UtcDateTime),
                ExamEndTime = envelope.Payload.CompletedAt,
                Status = MedicalRecordStatus.Finalized,
                WorkflowVersion = 2
            };
            db.MedicalRecords.Add(record);
        }
        else if (record.Status != MedicalRecordStatus.Finalized)
        {
            record.Status = MedicalRecordStatus.Finalized;
            record.ExamEndTime = envelope.Payload.CompletedAt;
            record.WorkflowVersion = 2;
            record.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else if (record.PatientId == Guid.Empty && envelope.Payload.PatientId != Guid.Empty)
        {
            record.PatientId = envelope.Payload.PatientId;
            record.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await MedicalHistoryProjector.UpsertAsync(db, record, ct);
        db.InboxMessages.Add(new InboxMessage { EventId = envelope.EventId, EventType = envelope.EventType, ProcessedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return true;
    }
}
