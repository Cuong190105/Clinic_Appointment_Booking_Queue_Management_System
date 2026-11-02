using System.Security.Claims;
using Clinic.Notification.Service.Endpoints;
using Clinic.Notification.Service.Messaging;
using Clinic.Notification.Service.Persistence;
using Clinic.Notification.Service.Realtime;
using Clinic.Notification.Service.Services;
using Clinic.Notification.Service.Services.Delivery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// JSON formatting
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Persistence
builder.Services.AddDbContext<NotificationDbContext>((sp, options) =>
{
    var env = sp.GetRequiredService<IHostEnvironment>();
    var config = sp.GetRequiredService<IConfiguration>();
    var conn = config.GetConnectionString("NotificationConnection")
        ?? config.GetConnectionString("DefaultConnection");

    if (!env.IsEnvironment("Testing") && !string.IsNullOrEmpty(conn))
    {
        options.UseSqlServer(conn);
    }
    else
    {
        var testDbName = config["TestDatabaseName"] ?? "ClinicNotificationDB_Fallback";
        options.UseInMemoryDatabase(testDbName);
    }
});

// Domain services & secret protection
builder.Services.AddNotificationDataProtection(builder.Configuration);
builder.Services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
builder.Services.AddSingleton<INotificationPolicyEngine, NotificationPolicyEngine>();
builder.Services.AddSingleton<INotificationEventProcessor, NotificationEventProcessor>();

// Background messaging consumers and dispatchers
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<NotificationEventHostedConsumer>();
    builder.Services.AddHostedService<NotificationOutboxDeliveryHostedService>();
}

// Delivery & Provider abstractions
builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();
builder.Services.AddScoped<ISmsSender, LoggingSmsSender>();
builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.AddScoped<NotificationDeliveryDispatcher>();

// SignalR Realtime Hub
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, SubUserIdProvider>();

// Authentication & JWT validation
var identityAuthority = builder.Configuration["Identity:Authority"] ?? "http://localhost:5001";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "http://localhost:5000";
var audience = builder.Configuration["Jwt:Audience"] ?? "Clinic.Services";
var requireHttpsMetadata = builder.Configuration.GetValue<bool>("Identity:RequireHttpsMetadata", false);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = identityAuthority.TrimEnd('/');
        options.RequireHttpsMetadata = requireHttpsMetadata;
        options.Audience = audience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = "sub"
        };

        // Extract token from query string for WebSocket SignalR connections
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Clinic Notification Service API (Internal)";
        document.Info.Version = "1.0.0";
        document.Info.Description = "Internal OpenAPI specification for Clinic.Notification.Service (in-app notifications, unread count, SignalR hub, and dead-letter delivery replay).";
        document.Components ??= new Microsoft.OpenApi.OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Name = "Authorization",
            Description = "JWT Bearer access token"
        };
        document.Components.SecuritySchemes["ServiceToken"] = new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Name = "X-Clinic-Service-Token",
            Description = "Internal service-to-service token"
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        var relativePath = "/" + (context.Description.RelativePath ?? string.Empty).TrimStart('/');
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var hasAuthorize = metadata.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any();
        if (relativePath.StartsWith("/internal/", StringComparison.OrdinalIgnoreCase))
        {
            operation.Security = new List<Microsoft.OpenApi.OpenApiSecurityRequirement>
            {
                new()
                {
                    [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("ServiceToken", context.Document)] = new List<string>()
                }
            };
        }
        else if (hasAuthorize)
        {
            operation.Security = new List<Microsoft.OpenApi.OpenApiSecurityRequirement>
            {
                new()
                {
                    [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", context.Document)] = new List<string>()
                }
            };
        }

        operation.Parameters ??= new List<Microsoft.OpenApi.IOpenApiParameter>();
        if (!operation.Parameters.Any(p => string.Equals(p.Name, "X-Correlation-ID", StringComparison.OrdinalIgnoreCase)))
        {
            operation.Parameters.Add(new Microsoft.OpenApi.OpenApiParameter
            {
                Name = "X-Correlation-ID",
                In = Microsoft.OpenApi.ParameterLocation.Header,
                Required = false,
                Description = "Distributed correlation identifier",
                Schema = new Microsoft.OpenApi.OpenApiSchema { Type = Microsoft.OpenApi.JsonSchemaType.String }
            });
        }
        return Task.CompletedTask;
    });
});

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi().AddEndpointFilter(async (context, next) =>
{
    var httpContext = context.HttpContext;
    var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
    var expectedToken = config["Identity:ServiceToken"] ?? config["Services:InternalToken"];
    var suppliedToken = httpContext.Request.Headers["X-Clinic-Service-Token"].FirstOrDefault()
        ?? httpContext.Request.Headers["X-Identity-Service-Token"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(expectedToken) && !string.IsNullOrWhiteSpace(suppliedToken) &&
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(expectedToken),
            System.Text.Encoding.UTF8.GetBytes(suppliedToken)))
    {
        return await next(context);
    }
    if (httpContext.User?.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    if (!httpContext.User.IsInRole("Admin")) return Results.Forbid();
    return await next(context);
});

app.MapGet("/", () => Results.Ok(new
{
    service = "Clinic.Notification.Service",
    status = "Healthy",
    version = "1.0.0"
}));

app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy", service = "Clinic.Notification.Service" }));

app.MapGet("/readyz", async (NotificationDbContext db, CancellationToken ct) =>
{
    try
    {
        if (db.Database.IsRelational() && !await db.Database.CanConnectAsync(ct))
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        return Results.Ok(new { status = "Ready", service = "Clinic.Notification.Service" });
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

// SignalR Hub & REST endpoints
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapNotificationEndpoints();
app.MapInternalNotificationEndpoints();

// Auto-migrate on relational startup
if (app.Environment.EnvironmentName != "Testing")
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync();
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not apply database migrations on startup.");
    }
}

app.Run();

public partial class Program { }
