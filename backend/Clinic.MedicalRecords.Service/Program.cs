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
    builder.Services.AddHostedService<PatientProjectionOutboxDispatcher>();
}

builder.Services.AddHttpClient("AppointmentProjection", client =>
{
    var authority = builder.Configuration["Appointment:Authority"] ?? "http://localhost:5003";
    client.BaseAddress = new Uri(authority.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(5);
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
    options.AddPolicy("ReportCreate", policy => policy.RequireAssertion(ctx =>
        ctx.User.IsInRole("Doctor") || ctx.User.HasClaim("permission", "medical-report.create")));
    options.AddPolicy("ReportView", policy => policy.RequireAssertion(ctx =>
        ctx.User.IsInRole("Doctor") || ctx.User.IsInRole("Admin") || ctx.User.IsInRole("Patient") ||
        ctx.User.HasClaim("permission", "medical-report.view.own") ||
        ctx.User.HasClaim("permission", "medical-report.view.related") ||
        ctx.User.HasClaim("permission", "medical-report.view.any")));
    options.AddPolicy("PatientHistoryView", policy => policy.RequireAssertion(ctx =>
        ctx.User.IsInRole("Doctor") || ctx.User.IsInRole("Admin") ||
        ctx.User.HasClaim("permission", "patient-history.view.related") ||
        ctx.User.HasClaim("permission", "patient-history.view.any")));
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

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Clinic Medical Records Service API (Internal)";
        document.Info.Version = "1.0.0";
        document.Info.Description = "Internal OpenAPI specification for Clinic.MedicalRecords.Service (patient projections, medical reports, finalize, amendments, and access audits).";
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
