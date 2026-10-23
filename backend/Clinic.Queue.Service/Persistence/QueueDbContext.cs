namespace Clinic.Queue.Service.Persistence;

using Clinic.Queue.Service.Entities;
using Microsoft.EntityFrameworkCore;

public class QueueDbContext : DbContext
{
    public QueueDbContext(DbContextOptions<QueueDbContext> options) : base(options)
    {
    }

    public DbSet<QueueTicket> QueueTickets => Set<QueueTicket>();
    public DbSet<QueueCounter> QueueCounters => Set<QueueCounter>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QueueDbContext).Assembly);
    }
}
