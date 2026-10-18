using Clinic.Infrastructure.Sqlserver.Inbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations;

public sealed class InboxMessageConfiguration
    : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ConsumerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.EventType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(x => new
        {
            x.EventId,
            x.ConsumerName
        })
        .IsUnique();

        builder.HasIndex(x => new
        {
            x.Status,
            x.ProcessingStartedAt
        });
    }
}
