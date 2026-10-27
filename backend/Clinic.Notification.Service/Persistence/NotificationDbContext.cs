using Clinic.Notification.Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Clinic.Notification.Service.Persistence;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<NotificationItem> Notifications => Set<NotificationItem>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<DeliveryOutboxMessage> DeliveryOutboxMessages => Set<DeliveryOutboxMessage>();
    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NotificationItem>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Body).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.ContactName).HasMaxLength(200);
            entity.Property(e => e.ContactEmail).HasMaxLength(200);
            entity.Property(e => e.ContactPhone).HasMaxLength(30);

            entity.HasIndex(e => new { e.RecipientId, e.CreatedAt });
            entity.HasIndex(e => new { e.RecipientId, e.IsRead });
            entity.HasIndex(e => e.AppointmentId);
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("InboxMessages");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EventType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
        });

        modelBuilder.Entity<DeliveryOutboxMessage>(entity =>
        {
            entity.ToTable("DeliveryOutboxMessages");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.IdempotencyKey).HasMaxLength(250).IsRequired();
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();

            entity.Property(e => e.RecipientAddress).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Subject).HasMaxLength(200);
            entity.Property(e => e.Content).HasMaxLength(4000).IsRequired();

            entity.HasIndex(e => new { e.Status, e.NextAttemptAt });

            entity.HasOne(e => e.Notification)
                  .WithMany(n => n.Deliveries)
                  .HasForeignKey(e => e.NotificationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeliveryAttempt>(entity =>
        {
            entity.ToTable("DeliveryAttempts");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProviderResponse).HasMaxLength(1000);
            entity.Property(e => e.ProviderReference).HasMaxLength(200);

            entity.HasOne(e => e.DeliveryOutboxMessage)
                  .WithMany(d => d.Attempts)
                  .HasForeignKey(e => e.DeliveryOutboxMessageId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

public class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NotificationDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost;Database=ClinicNotificationDB;User Id=sa;Password=ClinicDev!2026StrongPw;TrustServerCertificate=True;");
        return new NotificationDbContext(optionsBuilder.Options);
    }
}

