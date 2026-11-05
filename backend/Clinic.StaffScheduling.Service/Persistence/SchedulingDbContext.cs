using Clinic.StaffScheduling.Service.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinic.StaffScheduling.Service.Persistence;

public class SchedulingDbContext : DbContext
{
    public SchedulingDbContext(DbContextOptions<SchedulingDbContext> options) : base(options)
    {
    }

    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<WorkHistory> WorkHistories => Set<WorkHistory>();
    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
    public DbSet<ShiftRequest> ShiftRequests => Set<ShiftRequest>();
    public DbSet<SlotReservation> SlotReservations => Set<SlotReservation>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchedulingDbContext).Assembly);
    }
}

