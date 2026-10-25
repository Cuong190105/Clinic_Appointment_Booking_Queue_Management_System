namespace Clinic.Queue.Service.Persistence.Configurations;

using Clinic.Queue.Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.ConsumerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.EventType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(m => new { m.ConsumerName, m.EventId })
            .IsUnique()
            .HasDatabaseName("IX_InboxMessages_ConsumerName_EventId");
    }
}
