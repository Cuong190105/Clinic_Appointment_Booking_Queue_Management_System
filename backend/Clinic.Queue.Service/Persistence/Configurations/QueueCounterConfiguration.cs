namespace Clinic.Queue.Service.Persistence.Configurations;

using Clinic.Queue.Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class QueueCounterConfiguration : IEntityTypeConfiguration<QueueCounter>
{
    public void Configure(EntityTypeBuilder<QueueCounter> builder)
    {
        builder.ToTable("QueueCounters");

        builder.HasKey(c => new { c.DoctorId, c.QueueDate });

        builder.Property(c => c.QueueDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(c => c.LastNumber)
            .IsRequired();
    }
}
