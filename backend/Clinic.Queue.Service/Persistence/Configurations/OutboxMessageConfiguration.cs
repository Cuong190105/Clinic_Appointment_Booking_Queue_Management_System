namespace Clinic.Queue.Service.Persistence.Configurations;

using Clinic.Queue.Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.EventType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.Producer)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.RoutingKey)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(m => new { m.Status, m.NextAttemptAt })
            .HasDatabaseName("IX_OutboxMessages_Status_NextAttemptAt");
    }
}
