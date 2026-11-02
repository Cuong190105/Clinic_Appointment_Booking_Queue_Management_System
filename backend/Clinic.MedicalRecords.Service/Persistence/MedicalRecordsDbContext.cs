using Clinic.MedicalRecords.Service.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinic.MedicalRecords.Service.Persistence;

public sealed class MedicalRecordsDbContext(DbContextOptions<MedicalRecordsDbContext> options) : DbContext(options)
{
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<MedicalAuditEntry> AuditEntries => Set<MedicalAuditEntry>();
    public DbSet<MedicalHistoryEntry> MedicalHistory => Set<MedicalHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<MedicalRecord>().HasKey(x => x.Id);
        b.Entity<MedicalRecord>().HasIndex(x => x.AppointmentId).IsUnique();
        b.Entity<MedicalRecord>().HasIndex(x => new { x.PatientId, x.ExamDate });
        b.Entity<MedicalRecord>().Property(x => x.PatientName).HasMaxLength(200);
        b.Entity<MedicalRecord>().Property(x => x.DoctorName).HasMaxLength(200);
        b.Entity<MedicalRecord>().Property(x => x.SpecialtyName).HasMaxLength(200);
        b.Entity<MedicalRecord>().Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
        b.Entity<InboxMessage>().HasKey(x => x.EventId);
        b.Entity<InboxMessage>().Property(x => x.EventType).HasMaxLength(100);
        b.Entity<MedicalAuditEntry>().HasKey(x => x.Id);
        b.Entity<MedicalAuditEntry>().Property(x => x.Action).HasMaxLength(100);
        b.Entity<MedicalAuditEntry>().Property(x => x.ActorId).HasMaxLength(200);
        b.Entity<MedicalHistoryEntry>().HasKey(x => x.MedicalRecordId);
        b.Entity<MedicalHistoryEntry>().HasIndex(x => new { x.PatientId, x.ExamDate });
        b.Entity<MedicalHistoryEntry>().Property(x => x.DoctorName).HasMaxLength(200);
        b.Entity<MedicalHistoryEntry>().Property(x => x.SpecialtyName).HasMaxLength(200);
    }
}
