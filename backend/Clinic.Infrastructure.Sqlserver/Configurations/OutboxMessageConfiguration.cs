using Clinic.Contracts.Events;
using Clinic.Infrastructure.Sqlserver.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations;

public sealed class OutboxMessageConfiguration
    : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Producer)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.TraceParent)
            .HasMaxLength(200);

        builder.Property(x => x.TraceState)
            .HasMaxLength(1000);

        builder.Property(x => x.RoutingKey)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(x => new
        {
            x.Status,
            x.NextAttemptAt
        });

        builder.HasIndex(x => x.EventId)
            .IsUnique();

        builder.HasIndex(x => new
        {
            x.Status,
            x.NextAttemptAt,
            x.PublishingStartedAt
        });

        builder.HasIndex(x => new
        {
            x.Status,
            x.NextAttemptAt,
            x.LastAttemptAt
        });
    }
}
