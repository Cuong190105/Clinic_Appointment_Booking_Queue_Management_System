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
builder.Services.AddHttpClient<ISchedulingClient, SchedulingClient>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(schedulingBaseUrl);
    // Deadlines are enforced by the resilience pipeline below, not HttpClient.Timeout.
    client.Timeout = Timeout.InfiniteTimeSpan;
    var serviceToken = config["Services:InternalToken"] ?? config["Identity:ServiceToken"];
    if (!string.IsNullOrWhiteSpace(serviceToken))
    {
        client.DefaultRequestHeaders.Remove("X-Clinic-Service-Token");
        client.DefaultRequestHeaders.Add("X-Clinic-Service-Token", serviceToken);
    }
})
// Appointment -> Scheduling QoS (SLI_SLO_MATRIX §5.2): must fit inside the Gateway's 4s booking attempt.
// Total 3s, attempt 1.5s, 1 jittered retry. All Scheduling commands are idempotent (reserve by
// Idempotency-Key, confirm/release by reservation ID), so retrying POSTs is safe on this hop.
// Circuit breaker opens at >= 50% failures over 30s (min. 20 calls) for 15s -> booking fails fast with 503.
.AddStandardResilienceHandler(options =>
{
    var cfg = builder.Configuration;
    int Ms(string key, int fallback) => cfg.GetValue<int?>(key) is int v && v > 0 ? v : fallback;
    var total = Ms("QoS:Scheduling:TotalTimeoutMs", 3000);
    var attempt = Math.Min(Ms("QoS:Scheduling:AttemptTimeoutMs", 1500), total - 1);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMilliseconds(total);
    options.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(attempt);
    options.Retry.MaxRetryAttempts = Math.Max(1, cfg.GetValue<int?>("QoS:Scheduling:MaxRetryAttempts") ?? 1);
    options.Retry.Delay = TimeSpan.FromMilliseconds(100);
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = cfg.GetValue<int?>("QoS:Scheduling:BreakerMinimumThroughput") ?? 20;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.BreakDuration = TimeSpan.FromMilliseconds(Ms("QoS:Scheduling:BreakDurationMs", 15000));
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

// Map downstream (Scheduling) failures to explicit QoS responses instead of opaque 500s:
// circuit open -> 503 + Retry-After, deadline exceeded -> 504, connection failure -> 502.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex) when (!context.Response.HasStarted && Clinic.Appointment.Service.Common.DownstreamFailure.TryMap(ex, out var status, out var message, out var retryAfter))
    {
        context.Response.Clear();
        context.Response.StatusCode = status;
        if (retryAfter.HasValue) context.Response.Headers.RetryAfter = retryAfter.Value.ToString();
        context.Response.Headers[Clinic.Appointment.Service.Common.DownstreamFailure.DependencyFailureHeader] = "scheduling";
        await context.Response.WriteAsJsonAsync(new { title = "Downstream dependency failure", status, detail = message });
    }
});

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
