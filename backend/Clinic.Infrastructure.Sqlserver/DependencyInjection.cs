using Clinic.Application.Interfaces;
using Clinic.Infrastructure.Authentication;
using Clinic.Infrastructure.Sqlserver.Authentication;
using Clinic.Infrastructure.Sqlserver.Notifications;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Clinic.Infrastructure.Sqlserver.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Infrastructure.Sqlserver
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureSqlServer(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            Console.WriteLine($"Connection String: {connectionString}");
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            //services.AddScoped<IFarmRepository, FarmRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IPersonRepository, PersonRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<ITokenProvider, TokenProvider>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<ISpecialtyRepository, SpecialtyRepository>();
            services.AddScoped<IDoctorRepository, DoctorRepository>();
            services.AddScoped<IEmployeeRepository, EmployeeRepository>();
            services.AddScoped<IWorkScheduleRepository, WorkScheduleRepository>();
            services.AddScoped<IPatientRepository, PatientRepository>();
            services.AddScoped<IAppointmentRepository, AppointmentRepository>();

            services.AddScoped<DatabaseInitializer>();
            services.AddScoped<IAppointmentRepository, AppointmentRepository>();
            services.AddScoped<IMedicalReportRepository, MedicalReportRepository>();
            services.AddScoped<IQueueTicketRepository, QueueTicketRepository>();
            services.AddScoped<IPatientRepository, PatientRepository>();
            services.AddScoped<IDoctorRepository, DoctorRepository>();
            services.AddScoped<IWorkScheduleRepository, WorkScheduleRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IEmailSender, EmailSender>();
            services.AddScoped<ISmsSender, SmsSender>();

            return services;
        }
    }
}