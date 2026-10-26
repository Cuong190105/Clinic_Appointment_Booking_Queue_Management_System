using Clinic.MedicalRecords.Service.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinic.MedicalRecords.Service.Persistence;

public class MedicalRecordsDbContext : DbContext
{
    public MedicalRecordsDbContext(DbContextOptions<MedicalRecordsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<MedicalReport> MedicalReports => Set<MedicalReport>();
    public DbSet<MedicalReportAmendment> MedicalReportAmendments => Set<MedicalReportAmendment>();
    public DbSet<MedicalAccessAudit> MedicalAccessAudits => Set<MedicalAccessAudit>();
    public DbSet<EncounterContext> EncounterContexts => Set<EncounterContext>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Patients
        modelBuilder.Entity<Patient>(b =>
        {
            b.ToTable("Patients");
            b.HasKey(p => p.Id);

            b.Property(p => p.FullName).HasMaxLength(200).IsRequired();
            b.Property(p => p.PhoneNumber).HasMaxLength(20);
            b.Property(p => p.Email).HasMaxLength(200);
            b.Property(p => p.Gender).HasMaxLength(20).IsRequired();
            b.Property(p => p.Address).HasMaxLength(500);
            b.Property(p => p.InsuranceNumber).HasMaxLength(50);
            b.Property(p => p.EmergencyContact).HasMaxLength(50);

            b.HasIndex(p => p.IdentityUserId).IsUnique().HasFilter("[IdentityUserId] IS NOT NULL");
            b.HasIndex(p => p.PhoneNumber);
            b.HasIndex(p => p.FullName);
        });

        // EncounterContexts
        modelBuilder.Entity<EncounterContext>(b =>
        {
            b.ToTable("EncounterContexts");
            b.HasKey(e => e.AppointmentId);

            b.Property(e => e.DoctorName).HasMaxLength(200);
            b.Property(e => e.Status).HasMaxLength(20).IsRequired();

            b.HasIndex(e => e.PatientId);
            b.HasIndex(e => e.DoctorId);
            b.HasIndex(e => e.QueueDate);
        });

        // MedicalReports
        modelBuilder.Entity<MedicalReport>(b =>
        {
            b.ToTable("MedicalReports");
            b.HasKey(r => r.Id);

            b.Property(r => r.DoctorName).HasMaxLength(200);
            b.Property(r => r.SpecialtyName).HasMaxLength(200);
            b.Property(r => r.Symptoms).HasMaxLength(2000);
            b.Property(r => r.Diagnosis).HasMaxLength(2000);
            b.Property(r => r.Prescription).HasMaxLength(4000);
            b.Property(r => r.Notes).HasMaxLength(2000);
            b.Property(r => r.Status).HasMaxLength(20).IsRequired();

            // Strict unique constraint: One primary report per AppointmentId
            b.HasIndex(r => r.AppointmentId).IsUnique();

            b.HasOne(r => r.Patient)
                .WithMany(p => p.MedicalReports)
                .HasForeignKey(r => r.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(r => r.PatientId);
            b.HasIndex(r => r.DoctorId);
            b.HasIndex(r => r.ScheduleDate);
            b.HasIndex(r => r.Status);
        });

        // MedicalReportAmendments (append-only)
        modelBuilder.Entity<MedicalReportAmendment>(b =>
        {
            b.ToTable("MedicalReportAmendments");
            b.HasKey(a => a.Id);

            b.Property(a => a.Reason).HasMaxLength(1000).IsRequired();
            b.Property(a => a.Symptoms).HasMaxLength(2000);
            b.Property(a => a.Diagnosis).HasMaxLength(2000);
            b.Property(a => a.Prescription).HasMaxLength(4000);
            b.Property(a => a.Notes).HasMaxLength(2000);
            b.Property(a => a.AuthorName).HasMaxLength(200).IsRequired();

            b.HasOne(a => a.MedicalReport)
                .WithMany(r => r.Amendments)
                .HasForeignKey(a => a.MedicalReportId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(a => a.MedicalReportId);
            b.HasIndex(a => a.AppointmentId);
            b.HasIndex(a => new { a.MedicalReportId, a.AmendmentNumber }).IsUnique();
        });

        // MedicalAccessAudits
        modelBuilder.Entity<MedicalAccessAudit>(b =>
        {
            b.ToTable("MedicalAccessAudits");
            b.HasKey(a => a.Id);

            b.Property(a => a.ActorRole).HasMaxLength(50).IsRequired();
            b.Property(a => a.Action).HasMaxLength(50).IsRequired();
            b.Property(a => a.Outcome).HasMaxLength(20).IsRequired();
            b.Property(a => a.IpAddress).HasMaxLength(50);

            b.HasIndex(a => a.AppointmentId);
            b.HasIndex(a => a.PatientId);
            b.HasIndex(a => a.MedicalReportId);
            b.HasIndex(a => a.Timestamp);
            b.HasIndex(a => a.ActorUserId);
        });

        // InboxMessages
        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable("InboxMessages");
            b.HasKey(m => m.Id);

            b.Property(m => m.ConsumerName).HasMaxLength(100).IsRequired();
            b.Property(m => m.EventType).HasMaxLength(100).IsRequired();
            b.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);

            b.HasIndex(m => new { m.ConsumerName, m.EventId }).IsUnique();
            b.HasIndex(m => m.ReceivedAt);
        });
    }
}
