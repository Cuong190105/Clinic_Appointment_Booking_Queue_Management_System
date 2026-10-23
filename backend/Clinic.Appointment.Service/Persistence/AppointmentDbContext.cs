namespace Clinic.Appointment.Service.Persistence;

using Clinic.Appointment.Service.Entities;
using Microsoft.EntityFrameworkCore;

public class AppointmentDbContext : DbContext
{
    public AppointmentDbContext(DbContextOptions<AppointmentDbContext> options) : base(options)
    {
    }

    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentSnapshot> AppointmentSnapshots => Set<AppointmentSnapshot>();
    public DbSet<PatientProjection> PatientProjections => Set<PatientProjection>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppointmentDbContext).Assembly);
    }
}

