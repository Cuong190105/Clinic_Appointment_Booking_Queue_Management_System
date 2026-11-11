using System.Security.Claims;
using Clinic.Appointment.Service.Endpoints;
using Clinic.Appointment.Service.Persistence;
using Clinic.Appointment.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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
builder.Services.AddDbContext<AppointmentDbContext>((sp, options) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var conn = config.GetConnectionString("AppointmentConnection") ?? appointmentConnection;
    if (!string.IsNullOrEmpty(conn))
    {
        options.UseSqlServer(conn);
    }
});

// Outbox & Application Services
builder.Services.AddSingleton<IAppointmentWorkSignal, AppointmentWorkSignal>();
builder.Services.AddScoped<IAppointmentOutboxWriter, AppointmentOutboxWriter>();
builder.Services.AddScoped<IAppointmentManager, AppointmentManager>();
if (!builder.Environment.IsEnvironment("Testing") && !builder.Configuration.GetValue<bool>("SkipBackgroundWorkers"))
{
    builder.Services.AddHostedService<AppointmentOutboxPublisher>();
    builder.Services.AddHostedService<AppointmentConfirmationWorker>();
}
if (!builder.Environment.IsEnvironment("Testing") && !builder.Configuration.GetValue<bool>("SkipBackgroundWorkers"))
{
    builder.Services.AddHostedService<AppointmentEventConsumer>();
}

// HTTP Client for Staff & Scheduling Service
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

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Clinic Appointment Service API (Internal)";
        document.Info.Version = "1.0.0";
        document.Info.Description = "Internal OpenAPI specification for Clinic.Appointment.Service (booking saga, reschedule, check-in, receptionist walk-in, and analytics).";
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

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("clinic-appointment-service"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options => options.Filter = context => context.Request.Path != "/metrics")
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

var app = builder.Build();
app.MapPrometheusScrapingEndpoint();

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
        _ = await db.Appointments.AsNoTracking().AnyAsync();
        _ = await db.PatientProjections.AsNoTracking().AnyAsync();
    }
}

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
