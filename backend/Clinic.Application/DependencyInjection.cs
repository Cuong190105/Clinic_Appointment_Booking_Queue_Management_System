using System.Reflection;
using Clinic.Application.Interfaces;
using Clinic.Application.Notifications.Dispatchers;
using Clinic.Application.Notifications.Interfaces;
using Clinic.Application.Services;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            // ÄÄƒng kÃ½ toÃ n bá»™ Handler (IRequestHandler<...>) trong assembly nÃ y.
            // Khi nhÃ³m thÃªm Command/Query dÆ°á»›i Features/, MediatR sáº½ tá»± tÃ¬m tháº¥y,
            // khÃ´ng cáº§n Ä‘Äƒng kÃ½ thá»§ cÃ´ng tá»«ng handler.
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

            // Register Mapster Config
            var config = TypeAdapterConfig.GlobalSettings;
            config.Scan(Assembly.GetExecutingAssembly());
            services.AddSingleton(config);
            services.AddScoped<IMapper, Mapper>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IPersonService, PersonService>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddSingleton<INotificationQueue, NotificationQueue>();
            services.AddScoped<IAppointmentNotificationHandler, AppointmentNotificationHandler>();
            services.AddScoped<IAccountNotificationHandler, AccountNotificationHandler>();
            services.AddScoped<ICustomNotificationHandler, CustomNotificationHandler>();
            services.AddScoped<IDueNotificationHandler, DueNotificationHandler>();
            // Application services are registered via the infrastructure layer.
            // TODO: náº¿u dÃ¹ng Mapster theo pattern IRegister, quÃ©t cáº¥u hÃ¬nh mapping táº¡i Ä‘Ã¢y:
            // TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly());

            return services;
        }
    }
}
