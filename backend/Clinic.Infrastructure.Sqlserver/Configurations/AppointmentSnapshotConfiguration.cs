using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class AppointmentSnapshotConfiguration : IEntityTypeConfiguration<AppointmentSnapshot>
    {
        public void Configure(EntityTypeBuilder<AppointmentSnapshot> builder)
        {
            builder.ToTable("AppointmentSnapshots");

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedOnAdd();

            builder.HasOne(a => a.Appointment)
                .WithMany(a => a.Snapshots)
                .HasForeignKey(a => a.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Decouple WorkSchedule FK constraint across services
            builder.HasOne(a => a.OldWorkSchedule)
                .WithMany()
                .HasForeignKey(a => a.OldWorkScheduleId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

        }
    }
}
