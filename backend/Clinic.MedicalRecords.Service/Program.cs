using System.Security.Claims;
using Clinic.MedicalRecords.Service.Endpoints;
using Clinic.MedicalRecords.Service.Persistence;
using Clinic.MedicalRecords.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Configuration
var identityAuthority = builder.Configuration["Identity:Authority"] ?? "http://localhost:5001";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "http://localhost:5000";
var audience = builder.Configuration["Jwt:Audience"] ?? "Clinic.Services";
var requireHttpsMetadata = builder.Configuration.GetValue<bool>("Identity:RequireHttpsMetadata", false);
var medicalRecordsConnection = builder.Configuration.GetConnectionString("MedicalRecordsConnection");

// Persistence
builder.Services.AddDbContext<MedicalRecordsDbContext>((sp, options) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var conn = config.GetConnectionString("MedicalRecordsConnection");
    if (!string.IsNullOrEmpty(conn))
    {
        options.UseSqlServer(conn);
    }
});

// Application Services
builder.Services.AddScoped<IMedicalRecordsManager, MedicalRecordsManager>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<MedicalRecordsEventConsumer>();
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
    options.AddPolicy("ReportCreate", policy => policy.RequireAssertion(ctx =>
        ctx.User.IsInRole("Doctor") || ctx.User.HasClaim("permission", "medical-report.create")));
    options.AddPolicy("ReportView", policy => policy.RequireAssertion(ctx =>
        ctx.User.IsInRole("Doctor") || ctx.User.IsInRole("Admin") || ctx.User.IsInRole("Patient") ||
        ctx.User.HasClaim("permission", "medical-report.view.own") ||
        ctx.User.HasClaim("permission", "medical-report.view.related") ||
        ctx.User.HasClaim("permission", "medical-report.view.any")));
    options.AddPolicy("PatientViewAny", policy => policy.RequireAssertion(ctx =>
        ctx.User.IsInRole("Admin") || ctx.User.IsInRole("Receptionist") || ctx.User.IsInRole("Doctor") ||
        ctx.User.HasClaim("permission", "patient.view.any")));
    options.AddPolicy("PatientCreateAny", policy => policy.RequireAssertion(ctx =>
        ctx.User.IsInRole("Admin") || ctx.User.IsInRole("Receptionist") ||
        ctx.User.HasClaim("permission", "patient.create.any")));
    options.AddPolicy("PatientEditAny", policy => policy.RequireAssertion(ctx =>
        ctx.User.IsInRole("Admin") || ctx.User.IsInRole("Receptionist") ||
        ctx.User.HasClaim("permission", "patient.edit.any")));
    options.AddPolicy("PatientViewOwn", policy => policy.RequireAssertion(ctx =>
        ctx.User.IsInRole("Patient") || ctx.User.IsInRole("Admin") ||
        ctx.User.HasClaim("permission", "patient.view.own")));
    options.AddPolicy("PatientEditOwn", policy => policy.RequireAssertion(ctx =>
        ctx.User.IsInRole("Patient") || ctx.User.IsInRole("Admin") ||
        ctx.User.HasClaim("permission", "patient.edit.own")));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Health checks
app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy", service = "Clinic.MedicalRecords.Service" }));
app.MapGet("/readyz", async (MedicalRecordsDbContext db, CancellationToken ct) =>
{
    try
    {
        if (db.Database.IsRelational() && !await db.Database.CanConnectAsync(ct))
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        return Results.Ok(new { status = "Ready", service = "Clinic.MedicalRecords.Service" });
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapGet("/", () => Results.Ok(new { service = "Clinic.MedicalRecords.Service", version = "1.0.0" }));

// Authenticated introspection endpoint
app.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
{
    sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier),
    personId = user.FindFirstValue("personId"),
    patientId = user.FindFirstValue("patientId"),
    doctorId = user.FindFirstValue("doctorId"),
    employeeId = user.FindFirstValue("employeeId"),
    roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value)
})).RequireAuthorization();

// Map Medical Records & Patient APIs
app.MapMedicalRecordsEndpoints();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<MedicalRecordsDbContext>();
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
}

app.Run();

public partial class Program { }
