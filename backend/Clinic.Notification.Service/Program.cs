using System.Security.Claims;
using Clinic.Notification.Service.Entities;
using Clinic.API.Hubs;
using Clinic.Notification.Service.Persistence;
using Clinic.Notification.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<NotificationDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("NotificationConnection")));
builder.Services.AddHttpClient("sms", c => c.Timeout = TimeSpan.FromSeconds(15));
var signalR = builder.Services.AddSignalR(); var redis = builder.Configuration.GetConnectionString("Redis"); if (!string.IsNullOrWhiteSpace(redis)) signalR.AddStackExchangeRedis(redis);
builder.Services.AddSingleton<IDeliveryProvider, InAppProvider>(); builder.Services.AddSingleton<IDeliveryProvider, EmailProvider>(); builder.Services.AddSingleton<IDeliveryProvider, SmsProvider>();
builder.Services.AddScoped<NotificationIngestionService>(); builder.Services.AddScoped<DeliveryProcessor>();
builder.Services.AddHostedService<NotificationEventConsumer>(); builder.Services.AddHostedService<DeliveryWorker>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.Authority = (builder.Configuration["Identity:Authority"] ?? "http://localhost:5001").TrimEnd('/'); o.RequireHttpsMetadata = builder.Configuration.GetValue("Identity:RequireHttpsMetadata", false); o.Audience = builder.Configuration["Jwt:Audience"] ?? "Clinic.Services";
    o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuers = [builder.Configuration["Jwt:Issuer"] ?? "http://localhost:5000", "Clinic.Identity"], ValidateAudience = true,
        ValidAudiences = [builder.Configuration["Jwt:Audience"] ?? "Clinic.Services", "Clinic.Client"], ValidateLifetime = true, ValidateIssuerSigningKey = true, RoleClaimType = ClaimTypes.Role };
});
builder.Services.AddAuthorization(); builder.Services.AddOpenTelemetry().ConfigureResource(x => x.AddService("clinic-notification-service")).WithMetrics(x => x.AddAspNetCoreInstrumentation().AddMeter(NotificationTelemetry.MeterName).AddPrometheusExporter());
var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapPrometheusScrapingEndpoint(); app.MapHub<NotificationHub>("/hubs/notifications");
app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
app.MapGet("/readyz", async (NotificationDbContext db, CancellationToken ct) => { if (!await db.Database.CanConnectAsync(ct)) return Results.StatusCode(503); return Results.Ok(new { status = "Ready" }); }).AllowAnonymous();
app.MapGet("/me/notifications", async (ClaimsPrincipal user, DateTimeOffset? createdBefore, int? limit, NotificationDbContext db, CancellationToken ct) =>
{
    var raw = user.FindFirstValue("patientId") ?? user.FindFirstValue("personId") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
    if (!Guid.TryParse(raw, out var recipientId)) return Results.Forbid(); var before = createdBefore ?? DateTimeOffset.UtcNow; var take = Math.Clamp(limit ?? 20, 1, 100);
    var messages = await db.Messages.AsNoTracking().Where(x => x.RecipientId == recipientId && x.CreatedAt < before).OrderByDescending(x => x.CreatedAt).Take(take)
        .Select(x => new { x.Id, x.Title, message = x.Body, x.CreatedAt, sent = x.Deliveries.Any(d => d.Status == DeliveryStatus.Sent) }).ToListAsync(ct); return Results.Ok(messages);
}).RequireAuthorization();
app.MapGet("/admin/deliveries", async (DeliveryStatus? status, NotificationDbContext db, CancellationToken ct) =>
{
    var query = db.Deliveries.AsNoTracking().Include(x => x.Message).AsQueryable(); if (status.HasValue) query = query.Where(x => x.Status == status);
    return Results.Ok(await query.OrderByDescending(x => x.Message.CreatedAt).Take(200).Select(x => new { x.Id, x.Channel, x.Status, x.AttemptCount, x.NextAttemptAt, x.SentAt, x.ProviderResponseCode, x.LastErrorCode, x.Message.CorrelationId }).ToListAsync(ct));
}).RequireAuthorization(p => p.RequireRole("Admin"));
app.MapPost("/admin/deliveries/{id:guid}/replay", async (Guid id, NotificationDbContext db, CancellationToken ct) =>
{
    var delivery = await db.Deliveries.SingleOrDefaultAsync(x => x.Id == id, ct); if (delivery is null) return Results.NotFound(); if (delivery.Status != DeliveryStatus.DeadLettered) return Results.Conflict();
    delivery.Status = DeliveryStatus.Pending; delivery.AttemptCount = 0; delivery.NextAttemptAt = DateTimeOffset.UtcNow; delivery.LastErrorCode = null; await db.SaveChangesAsync(ct); return Results.Accepted();
}).RequireAuthorization(p => p.RequireRole("Admin"));
if (!app.Configuration.GetValue<bool>("SkipDatabaseInitialization")) { await using var scope = app.Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.EnsureCreatedAsync(); }
app.Run(); public partial class Program { }
