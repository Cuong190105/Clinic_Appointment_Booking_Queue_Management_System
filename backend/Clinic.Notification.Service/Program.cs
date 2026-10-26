using Clinic.Notification.Service.Persistence;
using Clinic.Notification.Service.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Persistence
builder.Services.AddDbContext<NotificationDbContext>((sp, options) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var conn = config.GetConnectionString("NotificationConnection")
        ?? config.GetConnectionString("DefaultConnection");

    if (!string.IsNullOrEmpty(conn))
    {
        options.UseSqlServer(conn);
    }
    else
    {
        options.UseInMemoryDatabase("ClinicNotificationDB_Fallback");
    }
});

// Domain services
builder.Services.AddSingleton<INotificationPolicyEngine, NotificationPolicyEngine>();

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

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
