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

            builder.HasOne(a => a.WorkSchedule)
                .WithMany(w => w.Appointments)
                .HasForeignKey(a => a.WorkScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(a => a.Updator)
                .WithMany()
                .HasForeignKey(a => a.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(a => a.Reason).HasMaxLength(500);

            builder.Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.HasIndex(a => new { a.TimeSlot, a.WorkScheduleId }).IsUnique().HasFilter("[IsWalkIn] = 0 AND [Status] <> 'Cancelled' AND [IsDeleted] = 0");

            // IsWalkIn: Ä‘Ã¡nh dáº¥u Appointment do Lá»… tÃ¢n táº¡o táº¡i quáº§y cho khÃ¡ch
            // vÃ£ng lai (CÃ¡ch B) - dÃ¹ng Ä‘á»ƒ loáº¡i khá»i thá»‘ng kÃª tá»· lá»‡ há»§y lá»‹ch.
            builder.Property(a => a.IsWalkIn).IsRequired();

            builder.Property(a => a.IdempotencyKey).HasMaxLength(100);
            builder.HasIndex(a => a.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");

            builder.HasOne(a => a.MedicalReport)
                .WithOne(m => m.Appointment)
                .HasForeignKey<MedicalReport>(m => m.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasData(SampleData.Appointments);
        }
    }
}
