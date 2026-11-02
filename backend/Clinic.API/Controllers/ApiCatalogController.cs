using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers;

/// <summary>
/// Provides catalog metadata, service topology, OpenAPI spec locations, and routing rules
/// for API consumers, frontend clients, and system auditors.
/// </summary>
[ApiController]
[Route("api/catalog")]
[AllowAnonymous]
public sealed class ApiCatalogController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ApiCatalogController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Returns the comprehensive microservices catalog, including endpoint routes,
    /// authority URLs, authorization requirements, and OpenAPI specification links.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiCatalogResponse), StatusCodes.Status200OK)]
    public IActionResult GetCatalog()
    {
        var catalog = new ApiCatalogResponse
        {
            GatewayVersion = "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            OpenApiSpecUrl = "/openapi/v1.json",
            SwaggerUiUrl = "/swagger/index.html",
            Services = new List<ServiceCatalogEntry>
            {
                new()
                {
                    Name = "Clinic.Identity.Service",
                    Role = "Authentication, Token Issuance, and JWKS Key Distribution",
                    Authority = _configuration["Identity:Authority"] ?? "http://localhost:5001",
                    HealthEndpoint = "/readyz",
                    RoutePrefixes = new[] { "/auth/login", "/auth/register", "/auth/refresh", "/auth/me", "/auth/jwks.json" },
                    AuthRequired = false,
                    OpenApiDocUrl = "/identity/openapi.json"
                },
                new()
                {
                    Name = "Clinic.Scheduling.Service",
                    Role = "Staff Scheduling, Doctor Shifts, Specialties, and Slot Allocation",
                    Authority = _configuration["Scheduling:Authority"] ?? "http://localhost:5002",
                    HealthEndpoint = "/readyz",
                    RoutePrefixes = new[] { "/employees", "/doctors", "/specialties", "/shifts", "/slots" },
                    AuthRequired = true,
                    OpenApiDocUrl = "/scheduling/openapi.json"
                },
                new()
                {
                    Name = "Clinic.Appointment.Service",
                    Role = "Appointment Booking, Rescheduling, Check-In, and Saga Orchestration",
                    Authority = _configuration["Appointment:Authority"] ?? "http://localhost:5003",
                    HealthEndpoint = "/readyz",
                    RoutePrefixes = new[] { "/appointments", "/reservations" },
                    AuthRequired = true,
                    OpenApiDocUrl = "/appointment/openapi.json"
                },
                new()
                {
                    Name = "Clinic.Queue.Service",
                    Role = "Daily Queue Ticket Issuance, Room Calling, Real-Time Board, Metrics",
                    Authority = _configuration["Queue:Authority"] ?? "http://localhost:5004",
                    HealthEndpoint = "/readyz",
                    RoutePrefixes = new[] { "/queue", "/admin/dashboard-metrics" },
                    AuthRequired = true,
                    OpenApiDocUrl = "/queue/openapi.json"
                },
                new()
                {
                    Name = "Clinic.MedicalRecords.Service",
                    Role = "Medical Encounter History, Examination Reports, and Patient Projections",
                    Authority = _configuration["MedicalRecords:Authority"] ?? "http://localhost:5005",
                    HealthEndpoint = "/readyz",
                    RoutePrefixes = new[] { "/medical-records", "/medical-reports" },
                    AuthRequired = true,
                    OpenApiDocUrl = "/medical-records/openapi.json"
                },
                new()
                {
                    Name = "Clinic.Notification.Service",
                    Role = "Omnichannel Notifications (Email, SMS, In-App, SignalR), Retry & DLQ",
                    Authority = _configuration["Notification:Authority"] ?? "http://localhost:5006",
                    HealthEndpoint = "/readyz",
                    RoutePrefixes = new[] { "/notifications", "/hubs/notifications" },
                    AuthRequired = true,
                    OpenApiDocUrl = "/notification/openapi.json"
                }
            },
            QoSPolicies = new QoSPolicySummary
            {
                MaxRequestTimeoutSeconds = 15,
                MaxRetryAttempts = 3,
                RateLimitingEnabled = true,
                RateLimitWindowMinutes = 1,
                DefaultRateLimitPermits = 100,
                CorrelationHeader = "X-Correlation-ID",
                IdempotencyHeader = "Idempotency-Key"
            }
        };

        return Ok(catalog);
    }
}

public sealed class ApiCatalogResponse
{
    public string GatewayVersion { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string OpenApiSpecUrl { get; set; } = string.Empty;
    public string SwaggerUiUrl { get; set; } = string.Empty;
    public List<ServiceCatalogEntry> Services { get; set; } = new();
    public QoSPolicySummary QoSPolicies { get; set; } = new();
}

public sealed class ServiceCatalogEntry
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public string HealthEndpoint { get; set; } = string.Empty;
    public string[] RoutePrefixes { get; set; } = Array.Empty<string>();
    public bool AuthRequired { get; set; }
    public string OpenApiDocUrl { get; set; } = string.Empty;
}

public sealed class QoSPolicySummary
{
    public int MaxRequestTimeoutSeconds { get; set; }
    public int MaxRetryAttempts { get; set; }
    public bool RateLimitingEnabled { get; set; }
    public int RateLimitWindowMinutes { get; set; }
    public int DefaultRateLimitPermits { get; set; }
    public string CorrelationHeader { get; set; } = string.Empty;
    public string IdempotencyHeader { get; set; } = string.Empty;
}
