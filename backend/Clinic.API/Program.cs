using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Clinic.API;
using Clinic.API.Middlewares;
using Clinic.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
const string IdentityJwtScheme = "IdentityJwt";

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

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        context.HttpContext.Response.ContentType = "application/json";

        var response = Clinic.API.Models.ApiResponse.Fail(
            HttpStatusCode.TooManyRequests,
            "Rate limit exceeded. Please try again after 60 seconds.");

        await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/readyz", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetNoLimiter("exempt");
        }

        var partitionKey = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub")
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue<int?>("QoS:RateLimitPermitLimit") ?? 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

// Add presentation services (Controllers, Filters, ProblemDetails, Mapster)
builder.Services.AddPresentation();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("clinic-api"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.Filter = context =>
                context.Request.Path != "/healthz" &&
                context.Request.Path != "/metrics";
        })
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

// Configure JSON serialization options to handle enum values as strings
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Correlation and User context
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Clinic.API.Middlewares.ICorrelationContext, Clinic.API.Middlewares.CorrelationContext>();
builder.Services.AddScoped<Clinic.API.Authentication.ICurrentUser, Clinic.API.Authentication.CurrentUser>();
builder.Services.AddTransient<Clinic.API.Middlewares.CorrelationIdDelegatingHandler>();

// Downstream microservice HTTP clients
builder.Services.AddHttpClient("Identity", client =>
{
    var identityAuthority = builder.Configuration["Identity:Authority"] ?? "http://localhost:5001";
    client.BaseAddress = new Uri(identityAuthority.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<Clinic.API.Middlewares.CorrelationIdDelegatingHandler>();

builder.Services.AddHttpClient("Scheduling", client =>
{
    var schedulingAuthority = builder.Configuration["Scheduling:Authority"] ?? "http://localhost:5002";
    client.BaseAddress = new Uri(schedulingAuthority.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<Clinic.API.Middlewares.CorrelationIdDelegatingHandler>();

builder.Services.AddHttpClient("Appointment", client =>
{
    var appointmentAuthority = builder.Configuration["Appointment:Authority"] ?? "http://localhost:5003";
    client.BaseAddress = new Uri(appointmentAuthority.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
}).AddHttpMessageHandler<Clinic.API.Middlewares.CorrelationIdDelegatingHandler>();

builder.Services.AddHttpClient("Queue", client =>
{
    var queueAuthority = builder.Configuration["Queue:Authority"] ?? "http://localhost:5004";
    client.BaseAddress = new Uri(queueAuthority.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
}).AddHttpMessageHandler<Clinic.API.Middlewares.CorrelationIdDelegatingHandler>();

builder.Services.AddHttpClient("MedicalRecords", client =>
{
    var medicalRecordsAuthority = builder.Configuration["MedicalRecords:Authority"]
        ?? builder.Configuration["Services:MedicalRecords:BaseUrl"]
        ?? "http://localhost:5005";
    client.BaseAddress = new Uri(medicalRecordsAuthority.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
}).AddHttpMessageHandler<Clinic.API.Middlewares.CorrelationIdDelegatingHandler>();

builder.Services.AddHttpClient("Notification", client =>
{
    var notificationAuthority = builder.Configuration["Notification:Authority"]
        ?? builder.Configuration["Services:Notification:BaseUrl"]
        ?? "http://localhost:5006";
    client.BaseAddress = new Uri(notificationAuthority.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<Clinic.API.Middlewares.CorrelationIdDelegatingHandler>();

// Gateway microservice client wrappers
builder.Services.AddTransient<IdentityAccountClient>();
builder.Services.AddTransient<SchedulingManagementClient>();
builder.Services.AddTransient<Clinic.API.Services.AppointmentServiceClient>();
builder.Services.AddTransient<Clinic.API.Services.QueueServiceClient>();
builder.Services.AddTransient<Clinic.API.Services.MedicalRecordsServiceClient>();
builder.Services.AddTransient<Clinic.API.Services.NotificationServiceClient>();

// Authentication with Identity Service JWKS / Authority
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = IdentityJwtScheme;
        options.DefaultChallengeScheme = IdentityJwtScheme;
    })
    .AddJwtBearer(IdentityJwtScheme, options =>
    {
        var identityAuthority = builder.Configuration["Identity:Authority"] ?? "http://localhost:5001";
        options.Authority = identityAuthority.TrimEnd('/');
        options.RequireHttpsMetadata = builder.Configuration.GetValue<bool?>("Identity:RequireHttpsMetadata")
            ?? (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"));
        options.Audience = builder.Configuration["Jwt:Audience"] ?? "Clinic.Services";
        var configuredIssuer = builder.Configuration["Jwt:Issuer"] ?? "http://localhost:5000";
        var configuredAudience = builder.Configuration["Jwt:Audience"] ?? "Clinic.Services";
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[] { configuredIssuer, "Clinic.Identity" },
            ValidateAudience = true,
            ValidAudiences = new[] { configuredAudience, "Clinic.Client" },
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/notifications"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Dynamic permission-based authorization
builder.Services.AddAuthorization();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, Clinic.API.Authorization.PermissionPolicyProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Clinic.API.Authorization.PermissionAuthorizationHandler>();

// OpenAPI & Swagger
builder.Services.AddOpenApi(Clinic.API.OpenApi.GatewayOpenApiConfiguration.Configure);
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
            [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
        });

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

// Microservice OpenAPI spec proxy routes declared in API Catalog (Service-Internal specs, restricted to Admin operators)
var adminOpenApiPolicy = new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { Roles = "Admin" };

static async Task<IResult> ProxyInternalServiceOpenApiAsync(
    string clientName,
    IHttpClientFactory clients,
    IConfiguration config,
    HttpContext httpContext,
    CancellationToken ct)
{
    try
    {
        var client = clients.CreateClient(clientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, "openapi/v1.json");
        var internalToken = config["Services:InternalToken"] ?? config["Identity:ServiceToken"];
        if (!string.IsNullOrWhiteSpace(internalToken))
        {
            request.Headers.TryAddWithoutValidation("X-Clinic-Service-Token", internalToken);
            request.Headers.TryAddWithoutValidation("X-Identity-Service-Token", internalToken);
        }
        if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) && !string.IsNullOrWhiteSpace(authHeader))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
        }
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return Results.StatusCode((int)response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(ct);
        return Results.Content(content, "application/json");
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}

app.MapGet("/identity/openapi.json", (IHttpClientFactory clients, IConfiguration config, HttpContext ctx, CancellationToken ct) =>
    ProxyInternalServiceOpenApiAsync("Identity", clients, config, ctx, ct)).RequireAuthorization(adminOpenApiPolicy).ExcludeFromDescription();

app.MapGet("/scheduling/openapi.json", (IHttpClientFactory clients, IConfiguration config, HttpContext ctx, CancellationToken ct) =>
    ProxyInternalServiceOpenApiAsync("Scheduling", clients, config, ctx, ct)).RequireAuthorization(adminOpenApiPolicy).ExcludeFromDescription();

app.MapGet("/appointment/openapi.json", (IHttpClientFactory clients, IConfiguration config, HttpContext ctx, CancellationToken ct) =>
    ProxyInternalServiceOpenApiAsync("Appointment", clients, config, ctx, ct)).RequireAuthorization(adminOpenApiPolicy).ExcludeFromDescription();

app.MapGet("/queue/openapi.json", (IHttpClientFactory clients, IConfiguration config, HttpContext ctx, CancellationToken ct) =>
    ProxyInternalServiceOpenApiAsync("Queue", clients, config, ctx, ct)).RequireAuthorization(adminOpenApiPolicy).ExcludeFromDescription();

app.MapGet("/medical-records/openapi.json", (IHttpClientFactory clients, IConfiguration config, HttpContext ctx, CancellationToken ct) =>
    ProxyInternalServiceOpenApiAsync("MedicalRecords", clients, config, ctx, ct)).RequireAuthorization(adminOpenApiPolicy).ExcludeFromDescription();

app.MapGet("/notification/openapi.json", (IHttpClientFactory clients, IConfiguration config, HttpContext ctx, CancellationToken ct) =>
    ProxyInternalServiceOpenApiAsync("Notification", clients, config, ctx, ct)).RequireAuthorization(adminOpenApiPolicy).ExcludeFromDescription();

app.Map("/hubs/notifications", (HttpContext ctx, NotificationServiceClient client, CancellationToken ct) =>
    client.ProxySignalRAsync(ctx, null, ct)).RequireAuthorization();
app.Map("/hubs/notifications/{**subPath}", (HttpContext ctx, string? subPath, NotificationServiceClient client, CancellationToken ct) =>
    client.ProxySignalRAsync(ctx, subPath, ct)).RequireAuthorization();
app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
app.MapGet("/readyz", async (IHttpClientFactory clients, CancellationToken ct) =>
{
    try
    {
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

// Global exception handling returning standard envelope ApiResponse
app.UseExceptionHandler();

app.Use((context, next) =>
{
    context.Request.EnableBuffering();
    return next();
});

app.UseMiddleware<RequestLoggingMiddleware>();

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
app.UseWebSockets();

app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
