using System.Reflection;
using Clinic.API.Common;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Clinic.Application.Interfaces;
using Clinic.API.Services;

namespace Clinic.API
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddPresentation(this IServiceCollection services)
        {
            // Register API controllers + tá»± Ä‘á»™ng bá»c má»i káº¿t quáº£ vÃ o ApiResponse.
            // Require authenticated users by default.
            services.AddControllers(options =>
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
                options.Filters.Add(new AuthorizeFilter(policy));
                options.Filters.Add<ApiResponseWrapperFilter>();
            });

            // Chuyá»ƒn má»i exception chÆ°a xá»­ lÃ½ thÃ nh envelope ApiResponse.
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddProblemDetails();

            // Set up Mapster configurations for API
            var config = TypeAdapterConfig.GlobalSettings;
            config.Scan(Assembly.GetExecutingAssembly());

            services.AddScoped<IInAppSender, InAppSender>();
            return services;
        }
    }
}
