using System.Security.Claims;
using Clinic.Appointment.Service.Endpoints;
using Clinic.Appointment.Service.Persistence;
using Clinic.Appointment.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
_ = Clinic.Appointment.Service.Common.ClinicTime.TimeZone; // Fail fast on an invalid clinic timezone.

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Configuration
var identityAuthority = builder.Configuration["Identity:Authority"] ?? "http://localhost:5001";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "http://localhost:5000";
var audience = builder.Configuration["Jwt:Audience"] ?? "Clinic.Services";
var requireHttpsMetadata = builder.Configuration.GetValue<bool>("Identity:RequireHttpsMetadata", false);
var appointmentConnection = builder.Configuration.GetConnectionString("AppointmentConnection");
var schedulingBaseUrl = builder.Configuration["Services:Scheduling:BaseUrl"]
    ?? builder.Configuration["Scheduling:Authority"]
    ?? "http://localhost:5002";

// Persistence
builder.Services.AddDbContext<AppointmentDbContext>(options =>
{
    if (!string.IsNullOrEmpty(appointmentConnection))
    {
        options.UseSqlServer(appointmentConnection);
    }
});

// Outbox & Application Services
builder.Services.AddScoped<IAppointmentOutboxWriter, AppointmentOutboxWriter>();
builder.Services.AddScoped<IAppointmentManager, AppointmentManager>();
builder.Services.AddHostedService<AppointmentOutboxPublisher>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<AppointmentEventConsumer>();
}

// HTTP Client for Scheduling Service
builder.Services.AddHttpClient<ISchedulingClient, SchedulingClient>(client =>
{
    client.BaseAddress = new Uri(schedulingBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

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
    options.AddPolicy("PatientAppointmentsViewAny", policy => policy.RequireAssertion(context =>
        context.User.IsInRole("Admin") ||
        context.User.HasClaim("permission", "patient.view.any")));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Health checks
app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy", service = "Clinic.Appointment.Service" }));
app.MapGet("/readyz", async (AppointmentDbContext db, CancellationToken ct) =>
{
    try
    {
        if (db.Database.IsRelational() && !await db.Database.CanConnectAsync(ct))
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        return Results.Ok(new { status = "Ready", service = "Clinic.Appointment.Service" });
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapGet("/", () => Results.Ok(new { service = "Clinic.Appointment.Service", version = "1.0.0" }));

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

// Appointment Business APIs
app.MapAppointmentApi();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppointmentDbContext>();
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
}

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
