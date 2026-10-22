using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
    {
        public void Configure(EntityTypeBuilder<Appointment> builder)
        {
            builder.ToTable("Appointments");

            builder.HasKey(a => a.Id);

            builder.HasOne(a => a.Patient)
                .WithMany(p => p.Appointments)
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Decouple WorkSchedule FK constraint across services
            builder.HasOne(a => a.WorkSchedule)
                .WithMany(w => w.Appointments)
                .HasForeignKey(a => a.WorkScheduleId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(a => a.DoctorId).IsRequired();
            builder.Property(a => a.DoctorName).HasMaxLength(200).IsRequired();
            builder.Property(a => a.SpecialtyName).HasMaxLength(100);
            builder.Property(a => a.ScheduleDate).IsRequired();

            builder.Property(a => a.Reason).HasMaxLength(500);

            builder.Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.HasIndex(a => new { a.TimeSlot, a.WorkScheduleId }).IsUnique().HasFilter("[IsWalkIn] = 0 AND [Status] <> 'Cancelled' AND [IsDeleted] = 0");

            // IsWalkIn: đánh dấu Appointment do Lễ tân tạo tại quầy cho khách
            // vãng lai (Cách B) - dùng để loại khỏi thống kê tỷ lệ hủy lịch.
            builder.Property(a => a.IsWalkIn).IsRequired();

            builder.HasData(SampleData.Appointments);
        }
    }
}
