using System.Reflection;
using Clinic.API.Common;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace Clinic.API
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddPresentation(this IServiceCollection services)
        {
            // Register API controllers + tự động bọc mọi kết quả vào ApiResponse.
            // Require authenticated users by default.
            services.AddControllers(options =>
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
                options.Filters.Add(new AuthorizeFilter(policy));
                options.Filters.Add<ApiResponseWrapperFilter>();
            });

            // Chuyển mọi exception chưa xử lý thành envelope ApiResponse.
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddProblemDetails();

            // Set up Mapster configurations for API
            var config = TypeAdapterConfig.GlobalSettings;
            config.Scan(Assembly.GetExecutingAssembly());

            return services;
        }
    }
}
