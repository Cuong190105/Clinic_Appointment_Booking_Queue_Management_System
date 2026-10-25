using Clinic.Scheduling.Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Scheduling.Service.Persistence.Configurations;

public class SpecialtyConfiguration : IEntityTypeConfiguration<Specialty>
{
    public void Configure(EntityTypeBuilder<Specialty> builder)
    {
        builder.ToTable("Specialties");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(150);
        builder.Property(s => s.Description).HasMaxLength(500);
        builder.Property(s => s.EstablishedDate).IsRequired();

        builder.HasIndex(s => s.Name).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PersonId).IsRequired();
        builder.Property(e => e.FullName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.PhoneNumber).HasMaxLength(20);
        builder.Property(e => e.Email).HasMaxLength(150);
        builder.Property(e => e.Gender).HasMaxLength(20);
        builder.Property(e => e.Address).HasMaxLength(500);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(e => e.PersonId).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.IdentityUserId);

        builder.HasOne(e => e.Manager)
            .WithMany()
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("Doctors");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.LicenseNumber).IsRequired().HasMaxLength(50);
        builder.Property(d => d.Qualification).HasMaxLength(300);
        builder.Property(d => d.Biography).HasMaxLength(1000);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(d => d.EmployeeId).IsUnique();
        builder.HasIndex(d => d.LicenseNumber).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(d => d.Employee)
            .WithOne(e => e.Doctor)
            .HasForeignKey<Doctor>(d => d.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkHistoryConfiguration : IEntityTypeConfiguration<WorkHistory>
{
    public void Configure(EntityTypeBuilder<WorkHistory> builder)
    {
        builder.ToTable("WorkHistories");
        builder.HasKey(w => w.Id);

        builder.HasOne(w => w.Doctor)
            .WithMany(d => d.WorkHistories)
            .HasForeignKey(w => w.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Specialty)
            .WithMany(s => s.WorkHistories)
            .HasForeignKey(w => w.SpecialtyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkScheduleConfiguration : IEntityTypeConfiguration<WorkSchedule>
{
    public void Configure(EntityTypeBuilder<WorkSchedule> builder)
    {
        builder.ToTable("WorkSchedules");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.CancellationReason).HasMaxLength(500);
        builder.Property(w => w.RecommendedCapacity).IsRequired();
        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(w => w.Doctor)
            .WithMany(d => d.WorkSchedules)
            .HasForeignKey(w => w.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(w => new { w.DoctorId, w.Date });
    }
}

public class ShiftRequestConfiguration : IEntityTypeConfiguration<ShiftRequest>
{
    public void Configure(EntityTypeBuilder<ShiftRequest> builder)
    {
        builder.ToTable("ShiftRequests");
        builder.HasKey(sr => sr.Id);
        builder.Property(sr => sr.Reason).HasMaxLength(500);
        builder.Property(sr => sr.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(sr => sr.Doctor)
            .WithMany(d => d.ShiftRequests)
            .HasForeignKey(sr => sr.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SlotReservationConfiguration : IEntityTypeConfiguration<SlotReservation>
{
    public void Configure(EntityTypeBuilder<SlotReservation> builder)
    {
        builder.ToTable("SlotReservations");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.IdempotencyKey).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(r => r.WorkSchedule)
            .WithMany(w => w.SlotReservations)
            .HasForeignKey(r => r.WorkScheduleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered unique index: Only one active (Reserved or Confirmed) reservation per (WorkScheduleId, TimeSlot)
        builder.HasIndex(r => new { r.WorkScheduleId, r.TimeSlot })
            .IsUnique()
            .HasFilter("[Status] IN ('Reserved', 'Confirmed')");

        // Unique index for idempotency
        builder.HasIndex(r => r.IdempotencyKey).IsUnique();

        // Index for expiration worker
        builder.HasIndex(r => new { r.Status, r.ExpiresAt });
    }
}

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.EventType).IsRequired().HasMaxLength(100);
        builder.Property(o => o.Producer).IsRequired().HasMaxLength(100);
        builder.Property(o => o.RoutingKey).IsRequired().HasMaxLength(100);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(30);

        builder.HasIndex(o => o.EventId).IsUnique();
        builder.HasIndex(o => new { o.Status, o.NextAttemptAt });
        builder.HasIndex(o => new { o.Status, o.CreatedAt });
    }
}

