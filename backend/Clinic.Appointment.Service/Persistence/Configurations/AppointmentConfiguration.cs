namespace Clinic.Appointment.Service.Persistence.Configurations;

using Clinic.Appointment.Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.PatientId).IsRequired();
        builder.Property(a => a.PatientName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.PatientPhone).HasMaxLength(20);
        builder.Property(a => a.PatientGender).HasMaxLength(10);

        builder.Property(a => a.WorkScheduleId).IsRequired();
        builder.Property(a => a.DoctorId).IsRequired();
        builder.Property(a => a.DoctorName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.SpecialtyName).HasMaxLength(100);
        builder.Property(a => a.ScheduleDate).IsRequired();

        builder.Property(a => a.TimeSlot).IsRequired();
        builder.Property(a => a.Reason).HasMaxLength(500);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.IsWalkIn).IsRequired();
        builder.Property(a => a.IdempotencyKey).HasMaxLength(128);
        builder.Property(a => a.UpdatedByUserId).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.IsDeleted).IsRequired().HasDefaultValue(false);

        // Unique index for idempotency key retry safety
        builder.HasIndex(a => a.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL AND [IsDeleted] = 0");

        // Unique index preventing overlapping bookings for the same doctor, date and time slot
        builder.HasIndex(a => new { a.DoctorId, a.ScheduleDate, a.TimeSlot })
            .IsUnique()
            .HasFilter("[Status] <> 'Cancelled' AND [IsDeleted] = 0 AND [IsWalkIn] = 0");

        // Indexes for fast lookup
        builder.HasIndex(a => new { a.PatientId, a.ScheduleDate });
        builder.HasIndex(a => new { a.DoctorId, a.ScheduleDate });
        builder.HasIndex(a => a.Status);

        builder.HasMany(a => a.Snapshots)
            .WithOne(s => s.Appointment)
            .HasForeignKey(s => s.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

