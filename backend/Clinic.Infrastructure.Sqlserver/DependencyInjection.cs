using Clinic.Application.Interfaces;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Clinic.Infrastructure.Sqlserver.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Clinic.Infrastructure.Sqlserver.Outbox;
using Clinic.Infrastructure.Sqlserver.Inbox;

namespace Clinic.Infrastructure.Sqlserver
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureSqlServer(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.Configure<RabbitMqOptions>(
                configuration.GetSection("RabbitMq"));
            services.Configure<RabbitMqConsumerOptions>(
                configuration.GetSection("RabbitMqConsumer"));

            services.AddSingleton<RabbitMqConnectionFactory>();

            services.AddScoped<IPersonRepository, PersonRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddScoped<DatabaseInitializer>();
            services.AddScoped<IOutboxWriter, OutboxWriter>();
            services.AddScoped<IEventContext, EventContext>();
            services.AddScoped<IInboxStore, InboxStore>();
            services.AddScoped<ICorrelationContext, CorrelationContext>();
            services.AddScoped<ISchedulingServiceClient, Services.SchedulingServiceClient>();
            return services;
        }
    }
}
