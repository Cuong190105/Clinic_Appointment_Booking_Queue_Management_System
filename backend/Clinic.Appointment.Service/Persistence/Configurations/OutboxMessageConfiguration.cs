namespace Clinic.Appointment.Service.Persistence.Configurations;

using Clinic.Appointment.Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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

