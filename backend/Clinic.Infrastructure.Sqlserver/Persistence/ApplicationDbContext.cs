using Clinic.Domain.Entities;
using Clinic.Infrastructure.Sqlserver.Outbox;
using Clinic.Infrastructure.Sqlserver.Inbox;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Clinic.Infrastructure.Sqlserver.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Person> Persons => Set<Person>();
        public DbSet<Patient> Patients => Set<Patient>();
        public DbSet<Appointment> Appointments => Set<Appointment>();
        public DbSet<QueueTicket> QueueTickets => Set<QueueTicket>();
        public DbSet<QueueCounter> QueueCounters => Set<QueueCounter>();
        public DbSet<MedicalReport> MedicalReports => Set<MedicalReport>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<AppointmentSnapshot> AppointmentSnapshots => Set<AppointmentSnapshot>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Tự động áp dụng mọi IEntityTypeConfiguration<T> trong assembly này
            // (toàn bộ file trong thư mục Configurations/).
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            base.OnModelCreating(modelBuilder);
        }
    }
}
