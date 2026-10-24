using Clinic.API;
using Clinic.API.Hubs;
using Clinic.API.Middlewares;
using Clinic.API.Workers;
using Clinic.API.Services;
using Clinic.Application;
using Clinic.Infrastructure.Sqlserver;
using Clinic.Infrastructure.Sqlserver.Notifications;
using Clinic.Infrastructure.Sqlserver.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Clinic.Infrastructure.Sqlserver.Outbox;
using Clinic.Domain.Common;
using Microsoft.OpenApi;
using System.Reflection;
using System.Security.Claims;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
_ = new TimeConverter(); // Fail fast when the clinic timezone is invalid.
const string ClinicJwtScheme = "ClinicJwt";
const string IdentityJwtScheme = "IdentityJwt";
const string LegacyJwtScheme = "LegacyJwt";

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:5174",
                "https://localhost:5173",
                "https://localhost:5174")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Add services to the container.
builder.Services.AddPresentation();
builder.Services.AddApplication();
builder.Services.AddInfrastructureSqlServer(builder.Configuration);

// Ensure the custom Meter and its instruments exist before the OpenTelemetry provider starts.
_ = MessagingTelemetry.Meter;
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("clinic-api"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter(MessagingTelemetry.MeterName)
        .AddPrometheusExporter())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.Filter = context =>
                context.Request.Path != "/healthz" &&
                context.Request.Path != "/metrics";
        })
        .AddHttpClientInstrumentation()
        .AddSource(MessagingTelemetry.SourceName)
        .AddOtlpExporter());

// Configure JSON serialization options to handle enum values as strings
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Configure notification services
builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection("Email"));
builder.Services.AddSignalR();

// Add workers. Test hosts can disable background consumers because their database
// and broker dependencies are outside the HTTP contract test boundary.
if (!builder.Configuration.GetValue<bool>("SkipBackgroundWorkers"))
{
    builder.Services.AddHostedService<NotificationWorker>();
    builder.Services.AddHostedService<ScheduledNotificationWorker>();
    builder.Services.AddHostedService<OutboxPublisher>();
    // AppointmentCheckedInConsumer removed: Queue Service is the sole consumer of AppointmentCheckedIn
}

// Add Authentication and Authorization services
builder.Services.AddHttpClient("Identity", client =>
{
    var identityAuthority = builder.Configuration["Identity:Authority"] ?? "http://localhost:5001";
    client.BaseAddress = new Uri(identityAuthority.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient("Scheduling", client =>
{
    var schedulingAuthority = builder.Configuration["Scheduling:Authority"] ?? "http://localhost:5002";
    client.BaseAddress = new Uri(schedulingAuthority.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient("Appointment", client =>
{
    var appointmentAuthority = builder.Configuration["Appointment:Authority"] ?? "http://localhost:5003";
    client.BaseAddress = new Uri(appointmentAuthority.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient("Queue", client =>
{
    var queueAuthority = builder.Configuration["Queue:Authority"] ?? "http://localhost:5004";
    client.BaseAddress = new Uri(queueAuthority.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddTransient<IdentityAccountClient>();
builder.Services.AddTransient<Clinic.API.Services.AppointmentServiceClient>();
builder.Services.AddTransient<Clinic.API.Services.QueueServiceClient>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = ClinicJwtScheme;
        options.DefaultChallengeScheme = ClinicJwtScheme;
    })
    .AddPolicyScheme(ClinicJwtScheme, "Clinic access token", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var header = context.Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return IdentityJwtScheme;
            try
            {
                var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
                    .ReadJwtToken(header["Bearer ".Length..].Trim());
                return string.IsNullOrWhiteSpace(token.Header.Kid) ? LegacyJwtScheme : IdentityJwtScheme;
            }
            catch
            {
                return IdentityJwtScheme;
            }
        };
    })
    .AddJwtBearer(IdentityJwtScheme, options =>
    {
        var identityAuthority = builder.Configuration["Identity:Authority"] ?? "http://localhost:5001";
        options.Authority = identityAuthority.TrimEnd('/');
        options.RequireHttpsMetadata = builder.Configuration.GetValue<bool?>("Identity:RequireHttpsMetadata")
            ?? (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"));
        options.Audience = builder.Configuration["Jwt:Audience"] ?? "Clinic.Services";
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "Clinic.Identity",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "Clinic.Services",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RoleClaimType = ClaimTypes.Role
        };
    })
    .AddJwtBearer(LegacyJwtScheme, options =>
    {
        var legacyKey = builder.Configuration["Jwt:Key"] ?? string.Empty;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = ["Clinic.API", "Clinic.Identity"],
            ValidateAudience = true,
            ValidAudiences = ["Clinic.Client", "Clinic.Services"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(legacyKey)),
            RoleClaimType = ClaimTypes.Role
        };
    });
// Register permission policy provider and handler for dynamic permission policies
builder.Services.AddAuthorization();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, Clinic.API.Authorization.PermissionPolicyProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Clinic.API.Authorization.PermissionAuthorizationHandler>();

// Register current-user accessor (đọc UserId từ ClaimsPrincipal của request hiện tại)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Clinic.Application.Interfaces.ICurrentUser, Clinic.API.Authentication.CurrentUser>();

// Cấu hình business rule của Appointment (vd: số giờ tối thiểu Patient phải đổi lịch trước hạn), đọc từ section "Appointment".
builder.Services.Configure<Clinic.API.Configuration.AppointmentPolicySettings>(
    builder.Configuration.GetSection(Clinic.API.Configuration.AppointmentPolicySettings.SectionName));
builder.Services.AddScoped<Clinic.Application.Interfaces.IAppointmentPolicySettings>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Clinic.API.Configuration.AppointmentPolicySettings>>().Value);

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] =
                new List<string>()
        });

    // Đọc file XML comment sinh ra từ bước 1, để Swagger UI hiện summary/description
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

app.MapPrometheusScrapingEndpoint();

var identityPublicBaseUrl = (builder.Configuration["Jwt:PublicBaseUrl"] ?? "http://localhost:5000").TrimEnd('/');
var identityIssuer = builder.Configuration["Jwt:Issuer"] ?? "http://localhost:5000";
app.MapGet("/.well-known/jwks.json", async (IHttpClientFactory clients, CancellationToken cancellationToken) =>
{
    using var response = await clients.CreateClient("Identity").GetAsync(".well-known/jwks.json", cancellationToken);
    if (!response.IsSuccessStatusCode) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    var jwks = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>(cancellationToken: cancellationToken);
    return jwks is null ? Results.StatusCode(StatusCodes.Status502BadGateway) : Results.Json(jwks.RootElement.Clone());
}).AllowAnonymous();

app.MapGet("/.well-known/openid-configuration", () => Results.Json(new
{
    issuer = identityIssuer,
    jwks_uri = $"{identityPublicBaseUrl}/.well-known/jwks.json",
    response_types_supported = new[] { "token" },
    subject_types_supported = new[] { "public" },
    id_token_signing_alg_values_supported = new[] { "RS256" },
    token_endpoint = $"{identityPublicBaseUrl}/auth/login"
})).AllowAnonymous();

app.MapGet("/schedules", async (IHttpClientFactory clients, HttpContext context) =>
{
    var client = clients.CreateClient("Scheduling");
    var queryString = context.Request.QueryString.Value ?? string.Empty;
    var response = await client.GetAsync($"schedules{queryString}");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json", statusCode: (int)response.StatusCode);
}).AllowAnonymous();

app.MapGet("/schedules/{id:guid}", async (Guid id, IHttpClientFactory clients) =>
{
    var client = clients.CreateClient("Scheduling");
    var response = await client.GetAsync($"schedules/{id}");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json", statusCode: (int)response.StatusCode);
}).AllowAnonymous();

app.MapHub<NotificationHub>("/hubs/notifications");
app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
app.MapGet("/readyz", async (IServiceScopeFactory scopeFactory, IHttpClientFactory clients, CancellationToken ct) =>
{
    try
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Clinic.Infrastructure.Sqlserver.Persistence.ApplicationDbContext>();
        if (!await db.Database.CanConnectAsync(ct)) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        using var identity = await clients.CreateClient("Identity").GetAsync("readyz", ct);
        return identity.IsSuccessStatusCode
            ? Results.Ok(new { status = "Ready" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

// Seed the database with an initial admin user if it doesn't exist.
// Integration contract tests intentionally skip this external infrastructure step.
if (!app.Configuration.GetValue<bool>("SkipDatabaseInitialization"))
{
    using var scope = app.Services.CreateScope();
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();

    await initializer.MigrateAsync();
}

// Bắt mọi exception chưa xử lý và trả về envelope ApiResponse (qua GlobalExceptionHandler).
app.UseExceptionHandler();

app.UseMiddleware<RequestLoggingMiddleware>();

// Configure the HTTP request pipeline.
if (true || app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("FrontendCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
