using Clinic.MedicalRecords.Service.Entities;
using Clinic.MedicalRecords.Service.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.MedicalRecords.Service.Services;

public static class MedicalHistoryProjector
{
    public static async Task UpsertAsync(MedicalRecordsDbContext db, MedicalRecord record, CancellationToken ct)
    {
        if (record.Status != MedicalRecordStatus.Finalized || record.PatientId == Guid.Empty)
            return;

        var entry = await db.MedicalHistory.SingleOrDefaultAsync(x => x.MedicalRecordId == record.Id, ct);
        if (entry is null)
        {
            entry = new MedicalHistoryEntry { MedicalRecordId = record.Id };
            db.MedicalHistory.Add(entry);
        }

        entry.AppointmentId = record.AppointmentId;
        entry.PatientId = record.PatientId;
        entry.DoctorId = record.DoctorId;
        entry.DoctorName = record.DoctorName;
        entry.SpecialtyName = record.SpecialtyName;
        entry.ExamDate = record.ExamDate;
        entry.Diagnosis = record.Diagnosis;
        entry.Prescription = record.Prescription;
        entry.Notes = record.Notes;
        entry.SourceVersion = record.WorkflowVersion;
        entry.ProjectedAt = DateTimeOffset.UtcNow;
    }

    public static async Task<int> RebuildAsync(MedicalRecordsDbContext db, CancellationToken ct)
    {
        if (db.Database.IsRelational())
            await db.MedicalHistory.ExecuteDeleteAsync(ct);
        else
            db.MedicalHistory.RemoveRange(await db.MedicalHistory.ToListAsync(ct));
        var records = await db.MedicalRecords
            .Where(x => x.Status == MedicalRecordStatus.Finalized && x.PatientId != Guid.Empty)
            .AsNoTracking()
            .ToListAsync(ct);
        foreach (var record in records)
            await UpsertAsync(db, record, ct);
        return records.Count;
    }
}
