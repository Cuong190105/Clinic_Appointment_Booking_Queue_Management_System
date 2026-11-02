using Clinic.Notification.Service.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Notification.Service.Persistence;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<NotificationMessage> Messages => Set<NotificationMessage>();
    public DbSet<NotificationDelivery> Deliveries => Set<NotificationDelivery>();
    public DbSet<DeliveryAttempt> Attempts => Set<DeliveryAttempt>();
    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<NotificationMessage>().HasKey(x => x.Id); b.Entity<NotificationMessage>().HasIndex(x => x.EventId).IsUnique();
        b.Entity<NotificationMessage>().Property(x => x.Title).HasMaxLength(300); b.Entity<NotificationMessage>().Property(x => x.TemplateKey).HasMaxLength(100);
        b.Entity<NotificationDelivery>().HasKey(x => x.Id); b.Entity<NotificationDelivery>().HasIndex(x => new { x.NotificationMessageId, x.Channel }).IsUnique();
        b.Entity<NotificationDelivery>().Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
        b.Entity<NotificationDelivery>().Property(x => x.RowVersion).IsRowVersion();
        b.Entity<DeliveryAttempt>().HasKey(x => x.Id); b.Entity<InboxMessage>().HasKey(x => x.EventId);
    }
}
