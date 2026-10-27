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

// Domain services
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

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

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
