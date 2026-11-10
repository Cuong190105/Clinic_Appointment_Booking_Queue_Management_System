using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class MedicalReportConfiguration : IEntityTypeConfiguration<MedicalReport>
    {
        public void Configure(EntityTypeBuilder<MedicalReport> builder)
        {
            builder.ToTable("MedicalReports");

            builder.HasKey(m => m.Id);

            // Gáº¯n trá»±c tiáº¿p qua AppointmentId
            builder.HasOne(m => m.Appointment)
                .WithOne(a => a.MedicalReport)
                .HasForeignKey<MedicalReport>(m => m.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(m => m.AppointmentId).IsUnique();

            builder.HasOne(m => m.QueueTicket)
                .WithOne(q => q.MedicalReport)
                .HasForeignKey<MedicalReport>(m => m.QueueTicketId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(m => m.Symptoms).HasMaxLength(1000);
            builder.Property(m => m.Diagnosis).HasMaxLength(1000);
            builder.Property(m => m.Prescription).HasMaxLength(2000);
            builder.Property(m => m.Notes).HasMaxLength(1000);

            builder.Property(m => m.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.HasMany(m => m.Amendments)
                .WithOne(a => a.MedicalReport)
                .HasForeignKey(a => a.MedicalReportId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class MedicalReportAmendmentConfiguration : IEntityTypeConfiguration<MedicalReportAmendment>
    {
        public void Configure(EntityTypeBuilder<MedicalReportAmendment> builder)
        {
            builder.ToTable("MedicalReportAmendments");

            builder.HasKey(a => a.Id);

            builder.Property(a => a.Reason).HasMaxLength(500).IsRequired();
            builder.Property(a => a.Content).HasMaxLength(2000).IsRequired();
            builder.Property(a => a.DoctorName).HasMaxLength(200).IsRequired();
        }
    }

    public class MedicalAccessAuditConfiguration : IEntityTypeConfiguration<MedicalAccessAudit>
    {
        public void Configure(EntityTypeBuilder<MedicalAccessAudit> builder)
        {
            builder.ToTable("MedicalAccessAudits");

            builder.HasKey(a => a.Id);

            builder.Property(a => a.Action).HasMaxLength(50).IsRequired();
            builder.Property(a => a.Details).HasMaxLength(1000);
            builder.HasIndex(a => a.MedicalReportId);
            builder.HasIndex(a => a.UserId);
        }
    }
}
