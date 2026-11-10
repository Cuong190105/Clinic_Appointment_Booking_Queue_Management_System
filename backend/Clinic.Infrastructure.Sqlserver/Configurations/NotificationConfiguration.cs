using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("Notifications");

            builder.HasKey(n => n.Id);

            // Gáº¯n theo PersonId (khÃ´ng pháº£i UserId) Ä‘á»ƒ gá»­i Ä‘Æ°á»£c cho cáº£
            // khÃ¡ch vÃ£ng lai chÆ°a tá»«ng táº¡o tÃ i khoáº£n.
            builder.HasOne(n => n.Person)
                .WithMany(p => p.Notifications)
                .HasForeignKey(n => n.PersonId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(n => n.Message)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(30);
            builder.Property(n => n.Channel).HasConversion<string>().HasMaxLength(20);
            builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(20);
        }
    }
}
