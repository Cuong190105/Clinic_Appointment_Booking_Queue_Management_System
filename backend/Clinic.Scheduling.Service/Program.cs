using System.Security.Claims;
using Clinic.Scheduling.Service.Endpoints;
using Clinic.Scheduling.Service.Persistence;
using Clinic.Scheduling.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Configuration
var identityAuthority = builder.Configuration["Identity:Authority"] ?? "http://localhost:5001";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "http://localhost:5000";
var audience = builder.Configuration["Jwt:Audience"] ?? "Clinic.Services";
var requireHttpsMetadata = builder.Configuration.GetValue<bool>("Identity:RequireHttpsMetadata", false);
var schedulingConnection = builder.Configuration.GetConnectionString("SchedulingConnection");

// Persistence
builder.Services.AddDbContext<SchedulingDbContext>(options =>
{
    if (!string.IsNullOrEmpty(schedulingConnection))
    {
        options.UseSqlServer(schedulingConnection);
    }
});

// Domain & Application Services
builder.Services.AddScoped<ISchedulingOutboxWriter, SchedulingOutboxWriter>();
builder.Services.AddScoped<ISchedulingManager, SchedulingManager>();
builder.Services.AddHostedService<ReservationExpirationWorker>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<SchedulingEventConsumer>();
    builder.Services.AddHostedService<SchedulingOutboxPublisher>();
}

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
    options.AddPolicy("SpecialtyManage", policy => policy.RequireAssertion(context =>
        context.User.IsInRole("Admin") || context.User.HasClaim("permission", "specialty.manage")));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Health checks
app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy", service = "Clinic.Scheduling.Service" }));
app.MapGet("/readyz", async (SchedulingDbContext db, CancellationToken ct) =>
{
    try
    {
        if (db.Database.IsRelational() && !await db.Database.CanConnectAsync(ct))
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        return Results.Ok(new { status = "Ready", service = "Clinic.Scheduling.Service" });
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapGet("/", () => Results.Ok(new { service = "Clinic.Scheduling.Service", version = "1.0.0" }));

// Authenticated introspection endpoint (verifies JWT & Claims against Identity JWKS)
app.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
{
    sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier),
    personId = user.FindFirstValue("personId"),
    doctorId = user.FindFirstValue("doctorId"),
    employeeId = user.FindFirstValue("employeeId"),
    roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value)
})).RequireAuthorization();

// Scheduling Business APIs
app.MapSchedulingApi();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
}

app.Run();

public partial class Program { }

