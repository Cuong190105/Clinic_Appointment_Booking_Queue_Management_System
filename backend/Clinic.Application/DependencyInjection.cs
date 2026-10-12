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
            // Đăng ký toàn bộ Handler (IRequestHandler<...>) trong assembly này.
            // Khi nhóm thêm Command/Query dưới Features/, MediatR sẽ tự tìm thấy,
            // không cần đăng ký thủ công từng handler.
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
            // TODO: nếu dùng Mapster theo pattern IRegister, quét cấu hình mapping tại đây:
            // TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly());

            return services;
        }
    }
}
