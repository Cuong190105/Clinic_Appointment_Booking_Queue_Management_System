using Clinic.Domain.Entities;
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
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<Doctor> Doctors => Set<Doctor>();
        public DbSet<Specialty> Specialties => Set<Specialty>();
        public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
        public DbSet<Patient> Patients => Set<Patient>();
        public DbSet<Appointment> Appointments => Set<Appointment>();
        public DbSet<QueueTicket> QueueTickets => Set<QueueTicket>();
        public DbSet<QueueCounter> QueueCounters => Set<QueueCounter>();
        public DbSet<MedicalReport> MedicalReports => Set<MedicalReport>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<WorkHistory> WorkHistories => Set<WorkHistory>();
        public DbSet<ShiftRequest> ShiftRequests => Set<ShiftRequest>();
        public DbSet<AppointmentSnapshot> AppointmentSnapshots => Set<AppointmentSnapshot>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Tự động áp dụng mọi IEntityTypeConfiguration<T> trong assembly này
            // (toàn bộ file trong thư mục Configurations/).
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            base.OnModelCreating(modelBuilder);
        }
    }
}
