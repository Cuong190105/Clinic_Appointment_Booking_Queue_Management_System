using System.Security.Claims;
using Clinic.Contracts.Events;
using Clinic.Contracts.Analytics;
using Clinic.Queue.Service.Common;
using Clinic.Queue.Service.Common.Exceptions;
using Clinic.Queue.Service.DTOs;
using Clinic.Queue.Service.Enums;
using Clinic.Queue.Service.Persistence;
using Clinic.Queue.Service.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
_ = ClinicTime.TimeZone; // Fail fast when the clinic timezone is invalid.

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Configuration
var identityAuthority = builder.Configuration["Identity:Authority"] ?? "http://localhost:5001";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "http://localhost:5000";
var audience = builder.Configuration["Jwt:Audience"] ?? "Clinic.Services";
var requireHttpsMetadata = builder.Configuration.GetValue<bool>("Identity:RequireHttpsMetadata", false);
var queueConnection = builder.Configuration.GetConnectionString("QueueConnection");

// Persistence
builder.Services.AddDbContext<QueueDbContext>((sp, options) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var conn = config.GetConnectionString("QueueConnection");
    if (!string.IsNullOrEmpty(conn))
    {
        options.UseSqlServer(conn);
    }
});

// Services
builder.Services.AddScoped<IQueueManager, QueueManager>();
builder.Services.AddHostedService<QueueOutboxPublisher>();
builder.Services.AddHostedService<QueueEventConsumer>();
builder.Services.AddHostedService<QueueMetricsWorker>();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("clinic-queue-service"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter(QueueTelemetry.MeterName)
        .AddPrometheusExporter())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options => options.Filter = context => context.Request.Path != "/metrics")
        .AddHttpClientInstrumentation()
        .AddSource(QueueTelemetry.ActivitySourceName)
        .AddOtlpExporter());

// Authentication via Identity Service JWKS
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
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DoctorOnly", policy => policy.RequireRole("Doctor"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("StaffOnly", policy => policy.RequireRole("Admin", "Receptionist", "Doctor"));
    options.AddPolicy("PatientOnly", policy => policy.RequireRole("Patient"));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapPrometheusScrapingEndpoint();

// Health checks
app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy", service = "Clinic.Queue.Service" }));
app.MapGet("/readyz", async (QueueDbContext db, ILogger<Program> logger, CancellationToken ct) =>
{
    try
    {
        if (db.Database.IsRelational() && !await db.Database.CanConnectAsync(ct))
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        return Results.Ok(new { status = "Ready", service = "Clinic.Queue.Service" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Readyz check failed");
        return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapGet("/", () => Results.Ok(new { service = "Clinic.Queue.Service", version = "1.0.0" }));

app.MapGet("/admin/dashboard-metrics", async (QueueDbContext db, CancellationToken ct) =>
{
    var today = ClinicTime.GetTodayQueueDate();
    var currentStart = today.AddDays(-30);
    var previousStart = today.AddDays(-60);

    var currentAverage = await db.QueueTickets
        .Where(ticket => !ticket.IsDeleted && ticket.Status == QueueStatus.Completed && ticket.QueueDate >= currentStart && ticket.QueueDate < today && ticket.CalledAt != null)
        .Select(ticket => (double?)EF.Functions.DateDiffMinute(ticket.CheckInTime, ticket.CalledAt!.Value))
        .AverageAsync(ct) ?? 0;
    var previousAverage = await db.QueueTickets
        .Where(ticket => !ticket.IsDeleted && ticket.Status == QueueStatus.Completed && ticket.QueueDate >= previousStart && ticket.QueueDate < currentStart && ticket.CalledAt != null)
        .Select(ticket => (double?)EF.Functions.DateDiffMinute(ticket.CheckInTime, ticket.CalledAt!.Value))
        .AverageAsync(ct) ?? 0;

    var change = previousAverage == 0
        ? (currentAverage == 0 ? 0 : 100)
        : (currentAverage - previousAverage) / previousAverage * 100;
    return Results.Ok(new QueueDashboardMetrics(Math.Round(currentAverage), change));
}).RequireAuthorization("AdminOnly");

// Authenticated introspection endpoint (verifies JWT & Claims against Identity JWKS)
app.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
{
    sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier),
    personId = user.FindFirstValue("personId"),
    patientId = user.FindFirstValue("patientId"),
    doctorId = user.FindFirstValue("doctorId"),
    employeeId = user.FindFirstValue("employeeId"),
    roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value)
})).RequireAuthorization();

// Queue Endpoints
app.MapGet("/doctors/{doctorId:guid}/queue", async (
    Guid doctorId,
    DateOnly? date,
    ClaimsPrincipal user,
    IQueueManager queueManager,
    CancellationToken ct) =>
{
    var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var isStaff = roles.Contains("Admin") || roles.Contains("Receptionist");
    if (!isStaff)
    {
        var doctorClaim = user.FindFirstValue("doctorId");
        if (string.IsNullOrEmpty(doctorClaim) || !Guid.TryParse(doctorClaim, out var docGuid) || docGuid != doctorId)
        {
            return Results.Forbid();
        }
    }

    var tickets = await queueManager.GetQueueByDoctorAsync(doctorId, date, ct);
    return Results.Ok(tickets);
}).RequireAuthorization();

app.MapPost("/doctors/{doctorId:guid}/queue/next", async (
    Guid doctorId,
    ClaimsPrincipal user,
    IQueueManager queueManager,
    CancellationToken ct) =>
{
    var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var isStaff = roles.Contains("Admin") || roles.Contains("Receptionist");
    if (!isStaff)
    {
        var doctorClaim = user.FindFirstValue("doctorId");
        if (string.IsNullOrEmpty(doctorClaim) || !Guid.TryParse(doctorClaim, out var docGuid) || docGuid != doctorId)
        {
            return Results.Forbid();
        }
    }

    try
    {
        var result = await queueManager.CallNextAsync(doctorId, null, ct);
        return Results.Ok(result);
    }
    catch (QueueConflictException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
    catch (QueueNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/queue/call-next/{doctorId:guid}", async (
    Guid doctorId,
    ClaimsPrincipal user,
    IQueueManager queueManager,
    CancellationToken ct) =>
{
    var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var isStaff = roles.Contains("Admin") || roles.Contains("Receptionist");
    if (!isStaff)
    {
        var doctorClaim = user.FindFirstValue("doctorId");
        if (string.IsNullOrEmpty(doctorClaim) || !Guid.TryParse(doctorClaim, out var docGuid) || docGuid != doctorId)
        {
            return Results.Forbid();
        }
    }

    try
    {
        var result = await queueManager.CallNextAsync(doctorId, null, ct);
        return Results.Ok(result);
    }
    catch (QueueConflictException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
    catch (QueueNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/queue/{queueTicketId:guid}/start-exam", async (
    Guid queueTicketId,
    ClaimsPrincipal user,
    IQueueManager queueManager,
    CancellationToken ct) =>
{
    var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var hasAnyScope = roles.Contains("Admin") || roles.Contains("Receptionist");
    Guid? actorDoctorId = null;
    var doctorClaim = user.FindFirstValue("doctorId");
    if (!string.IsNullOrEmpty(doctorClaim) && Guid.TryParse(doctorClaim, out var dId))
    {
        actorDoctorId = dId;
    }

    try
    {
        await queueManager.StartExamAsync(queueTicketId, actorDoctorId, hasAnyScope, ct);
        return Results.NoContent();
    }
    catch (QueueForbiddenException)
    {
        return Results.Forbid();
    }
    catch (QueueNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/queue/{queueTicketId:guid}/complete-exam", async (
    Guid queueTicketId,
    ClaimsPrincipal user,
    IQueueManager queueManager,
    CancellationToken ct) =>
{
    var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var hasAnyScope = roles.Contains("Admin") || roles.Contains("Receptionist");
    Guid? actorDoctorId = null;
    var doctorClaim = user.FindFirstValue("doctorId");
    if (!string.IsNullOrEmpty(doctorClaim) && Guid.TryParse(doctorClaim, out var dId))
    {
        actorDoctorId = dId;
    }

    try
    {
        await queueManager.CompleteExamAsync(queueTicketId, actorDoctorId, hasAnyScope, ct);
        return Results.NoContent();
    }
    catch (QueueForbiddenException)
    {
        return Results.Forbid();
    }
    catch (QueueNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/queue/{queueTicketId:guid}/skip", async (
    Guid queueTicketId,
    ClaimsPrincipal user,
    IQueueManager queueManager,
    CancellationToken ct) =>
{
    var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var isStaff = roles.Contains("Admin") || roles.Contains("Receptionist");
    if (!isStaff)
    {
        var ticket = await queueManager.GetByIdAsync(queueTicketId, ct);
        if (ticket == null) return Results.NotFound();

        var doctorClaim = user.FindFirstValue("doctorId");
        if (string.IsNullOrEmpty(doctorClaim) || !Guid.TryParse(doctorClaim, out var docGuid) || docGuid != ticket.DoctorId)
        {
            return Results.Forbid();
        }
    }

    try
    {
        var result = await queueManager.SkipAsync(queueTicketId, ct);
        return Results.Ok(result);
    }
    catch (QueueNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapMethods("/queue/{queueTicketId:guid}/priority", new[] { "PATCH", "POST" }, async (
    Guid queueTicketId,
    SetQueuePriorityRequest request,
    ClaimsPrincipal user,
    IQueueManager queueManager,
    CancellationToken ct) =>
{
    var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var isStaff = roles.Contains("Admin") || roles.Contains("Receptionist");
    if (!isStaff)
    {
        var ticket = await queueManager.GetByIdAsync(queueTicketId, ct);
        if (ticket == null) return Results.NotFound();

        var doctorClaim = user.FindFirstValue("doctorId");
        if (string.IsNullOrEmpty(doctorClaim) || !Guid.TryParse(doctorClaim, out var docGuid) || docGuid != ticket.DoctorId)
        {
            return Results.Forbid();
        }
    }

    try
    {
        await queueManager.SetPriorityAsync(queueTicketId, request.Priority, ct);
        return Results.NoContent();
    }
    catch (QueueNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapGet("/me/queue-status", async (
    ClaimsPrincipal user,
    IQueueManager queueManager,
    CancellationToken ct) =>
{
    var patientClaim = user.FindFirstValue("patientId");
    if (string.IsNullOrEmpty(patientClaim) || !Guid.TryParse(patientClaim, out var patientId))
    {
        return Results.Forbid();
    }

    var result = await queueManager.GetQueueStatusByPatientAsync(patientId, null, ct);
    return result.Count == 0 ? Results.NoContent() : Results.Ok(result);
}).RequireAuthorization();

app.MapGet("/queue/{queueTicketId:guid}", async (
    Guid queueTicketId,
    ClaimsPrincipal user,
    IQueueManager queueManager,
    CancellationToken ct) =>
{
    var ticket = await queueManager.GetByIdAsync(queueTicketId, ct);
    if (ticket == null) return Results.NotFound();

    var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (!roles.Contains("Admin") && !roles.Contains("Receptionist"))
    {
        if (roles.Contains("Doctor"))
        {
            var doctorClaim = user.FindFirstValue("doctorId");
            if (string.IsNullOrEmpty(doctorClaim) || !Guid.TryParse(doctorClaim, out var dId) || dId != ticket.DoctorId)
            {
                return Results.Forbid();
            }
        }
        else if (roles.Contains("Patient"))
        {
            var patientClaim = user.FindFirstValue("patientId") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            if (string.IsNullOrEmpty(patientClaim) || !Guid.TryParse(patientClaim, out var pId) || pId != ticket.PatientId)
            {
                return Results.Forbid();
            }
        }
        else
        {
            return Results.Forbid();
        }
    }

    return Results.Ok(ticket);
}).RequireAuthorization();

app.MapGet("/queue/appointment/{appointmentId:guid}", async (
    Guid appointmentId,
    ClaimsPrincipal user,
    IQueueManager queueManager,
    CancellationToken ct) =>
{
    var ticket = await queueManager.GetByAppointmentIdAsync(appointmentId, ct);
    if (ticket == null) return Results.NotFound();

    var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (!roles.Contains("Admin") && !roles.Contains("Receptionist"))
    {
        if (roles.Contains("Doctor"))
        {
            var doctorClaim = user.FindFirstValue("doctorId");
            if (string.IsNullOrEmpty(doctorClaim) || !Guid.TryParse(doctorClaim, out var dId) || dId != ticket.DoctorId)
            {
                return Results.Forbid();
            }
        }
        else if (roles.Contains("Patient"))
        {
            var patientClaim = user.FindFirstValue("patientId") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            if (string.IsNullOrEmpty(patientClaim) || !Guid.TryParse(patientClaim, out var pId) || pId != ticket.PatientId)
            {
                return Results.Forbid();
            }
        }
        else
        {
            return Results.Forbid();
        }
    }

    return Results.Ok(ticket);
}).RequireAuthorization();

if (!app.Environment.IsEnvironment("Testing"))
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QueueDbContext>();
        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync();
        }
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Failed to apply database migrations on startup.");
    }
}

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
