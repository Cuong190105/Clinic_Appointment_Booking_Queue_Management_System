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

            // Gắn qua QueueTicket (không phải Appointment trực tiếp) - vì mọi
            // ca khám đều phải qua hàng đợi trước khi bác sĩ ghi nhận kết quả.
            builder.HasOne(m => m.QueueTicket)
                .WithOne(q => q.MedicalReport)
                .HasForeignKey<MedicalReport>(m => m.QueueTicketId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(m => m.QueueTicketId).IsUnique();

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
