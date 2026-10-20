using Clinic.Application.Interfaces;
using Clinic.Infrastructure.Sqlserver.Notifications;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Clinic.Infrastructure.Sqlserver.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Clinic.Infrastructure.Sqlserver.Outbox;
using Clinic.Infrastructure.Sqlserver.Inbox;
// using System.Text.Json;

namespace Clinic.Infrastructure.Sqlserver
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureSqlServer(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            // Console.WriteLine($"Connection String: {JsonSerializer.Serialize(connectionString)}");
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.Configure<RabbitMqOptions>(
                configuration.GetSection("RabbitMq"));
            services.Configure<RabbitMqConsumerOptions>(
                configuration.GetSection("RabbitMqConsumer"));

            services.AddSingleton<RabbitMqConnectionFactory>();

            //services.AddScoped<IFarmRepository, FarmRepository>();
            services.AddScoped<IPersonRepository, PersonRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
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
            services.AddScoped<IOutboxWriter, OutboxWriter>();
            services.AddScoped<IEventContext, EventContext>();
            services.AddScoped<IInboxStore, InboxStore>();
            services.AddScoped<ICorrelationContext, CorrelationContext>();
            return services;
        }
    }
}
