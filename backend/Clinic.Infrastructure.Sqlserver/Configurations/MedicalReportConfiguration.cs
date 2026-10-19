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

            builder.HasOne(m => m.Appointment)
                .WithOne(a => a.MedicalReport)
                .HasForeignKey<MedicalReport>(m => m.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(m => m.AppointmentId).IsUnique();

            builder.HasOne(m => m.Patient)
                .WithMany()
                .HasForeignKey(m => m.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(m => m.Symptoms).HasMaxLength(1000);
            builder.Property(m => m.Diagnosis).HasMaxLength(1000);
            builder.Property(m => m.Prescription).HasMaxLength(2000);
            builder.Property(m => m.Notes).HasMaxLength(1000);

            builder.Property(m => m.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.HasData(SampleData.MedicalReports);
        }
    }
}
