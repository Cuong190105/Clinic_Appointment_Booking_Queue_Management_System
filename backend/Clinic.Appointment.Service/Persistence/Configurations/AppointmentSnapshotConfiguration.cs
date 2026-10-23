namespace Clinic.Appointment.Service.Persistence.Configurations;

using Clinic.Appointment.Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AppointmentSnapshotConfiguration : IEntityTypeConfiguration<AppointmentSnapshot>
{
    public void Configure(EntityTypeBuilder<AppointmentSnapshot> builder)
    {
        builder.ToTable("AppointmentSnapshots");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedOnAdd();

        builder.Property(s => s.OldDoctorName).HasMaxLength(200);
        builder.Property(s => s.OldSpecialtyName).HasMaxLength(100);
        builder.Property(s => s.Reason).HasMaxLength(500);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.UpdatedByUserName).HasMaxLength(200);

        builder.HasIndex(s => s.AppointmentId);
    }
}

