using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class WorkScheduleConfiguration : IEntityTypeConfiguration<WorkSchedule>
    {
        public void Configure(EntityTypeBuilder<WorkSchedule> builder)
        {
            builder.ToTable("WorkSchedules");

            builder.HasKey(w => w.Id);

            builder.HasOne(w => w.Doctor)
                .WithMany(d => d.WorkSchedules)
                .HasForeignKey(w => w.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(w => w.CancellationReason)
                .HasMaxLength(500);

            builder.Property(w => w.PatientLimit).IsRequired();

            builder.Property(w => w.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.HasData(SampleData.WorkSchedules);
        }
    }
}
