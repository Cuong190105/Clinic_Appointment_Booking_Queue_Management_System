using Clinic.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers;

/// <summary>
/// Provides catalog metadata, service topology, OpenAPI spec locations, visibility classifications,
/// and routing rules for API consumers, frontend clients, and system auditors.
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
    [ProducesResponseType(typeof(ApiResponse<ApiCatalogResponse>), StatusCodes.Status200OK)]
    public IActionResult GetCatalog()
    {
        var catalog = new ApiCatalogResponse
        {
            GatewayVersion = "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            OpenApiSpecUrl = "/openapi/v1.json",
            OpenApiSpecVisibility = "Public",
            SwaggerUiUrl = "/swagger/index.html",
            Services = new List<ServiceCatalogEntry>
            {
                new()
                {
                    Name = "Clinic.Identity.Service",
                    Role = "Authentication, Token Issuance, and JWKS Key Distribution",
                    Authority = ResolveAuthority("Identity"),
                    AuthorityScope = "InternalClusterOnly",
                    HealthEndpoint = "/readyz",
                    RoutePrefixes = new[]
                    {
                        "/auth/login",
                        "/auth/register",
                        "/auth/refresh",
                        "/auth/logout",
                        "/auth/revoke",
                        "/auth/me",
                        "/auth/permissions",
                        "/forgot-password",
                        "/reset-password",
                        "/change-password",
                        "/admin/users",
                        "/admin/roles"
                    },
                    AuthPolicy = "Mixed",
                    AnonymousRoutes = new[]
                    {
                        "GET /.well-known/jwks.json",
                        "GET /.well-known/openid-configuration",
                        "POST /auth/login",
                        "POST /auth/register",
                        "POST /auth/refresh",
                        "POST /auth/logout",
                        "POST /auth/revoke",
                        "POST /forgot-password",
                        "POST /reset-password"
                    },
                    AuthenticatedRoutes = new[]
                    {
                        "GET /auth/me",
                        "GET /auth/permissions",
                        "POST /change-password",
                        "GET /admin/roles",
                        "GET /admin/users",
                        "GET /admin/users/all-brief",
                        "GET /admin/users/{userId}"
                    },
                    SpecVisibility = "Internal",
                    OpenApiAccessPolicy = "Admin",
                    OpenApiDocUrl = "/identity/openapi.json"
                },
                new()
                {
                    Name = "Clinic.StaffScheduling.Service",
                    Role = "Staff Scheduling, Doctor Shifts, Specialties, and Slot Allocation",
                    Authority = ResolveAuthority("Scheduling"),
                    AuthorityScope = "InternalClusterOnly",
                    HealthEndpoint = "/readyz",
                    RoutePrefixes = new[] { "/specialties", "/doctors", "/employees", "/schedules", "/shifts", "/slots" },
                    AuthPolicy = "Mixed",
                    AnonymousRoutes = new[]
                    {
                        "GET /specialties",
                        "GET /specialties/{id}",
                        "GET /doctors",
                        "GET /doctors/{id}",
                        "GET /doctors/available",
                        "GET /doctors/{id}/available",
                        "GET /doctors/{doctorId}/shifts",
                        "GET /doctors/dashboard-stats",
                        "GET /schedules",
                        "GET /schedules/{id}",
                        "GET /schedules/availability",
                        "GET /slots"
                    },
                    AuthenticatedRoutes = new[]
                    {
                        "POST /specialties",
                        "PUT /specialties/{id}",
                        "PATCH /specialties/{id}/status",
                        "GET /employees",
                        "GET /employees/me",
                        "GET /employees/{id}",
                        "POST /employees",
                        "POST /employees/from-user",
                        "PUT /employees/{employeeId}",
                        "PUT /employees/me",
                        "POST /employees/{id}/status",
                        "POST /doctors",
                        "POST /doctors/from-user",
                        "PUT /doctors/{doctorId}",
                        "PUT /doctors/me",
                        "GET /doctors/me",
                        "GET /doctors/shifts",
                        "GET /doctors/{doctorId}/suggestions",
                        "POST /doctors/{doctorId}/shifts",
                        "PATCH /doctors/{id}/status",
                        "POST /doctors/{id}/status",
                        "PATCH /doctors/{id}/specialty",
                        "POST /doctors/{id}/specialty",
                        "GET /admin/doctors/brief",
                        "GET /shifts",
                        "GET /shifts/suggestions",
                        "POST /shifts/suggestions",
                        "PATCH /shifts/suggestions/{id}",
                        "POST /shifts/suggestions/{id}/cancel",
                        "POST /shifts/suggestions/{id}/approve",
                        "POST /shifts/suggestions/{id}/reject",
                        "PUT /shifts/{id}",
                        "POST /shifts/{id}/cancel",
                        "POST /schedules/{scheduleId}/reservations",
                        "POST /schedules/{scheduleId}/reservations/{reservationId}/confirm",
                        "POST /schedules/{scheduleId}/reservations/{reservationId}/release"
                    },
                    SpecVisibility = "Internal",
                    OpenApiAccessPolicy = "Admin",
                    OpenApiDocUrl = "/scheduling/openapi.json"
                },
                new()
                {
                    Name = "Clinic.Appointment.Service",
                    Role = "Appointment Booking, Rescheduling, Check-In, and Saga Orchestration",
                    Authority = ResolveAuthority("Appointment"),
                    AuthorityScope = "InternalClusterOnly",
                    HealthEndpoint = "/readyz",
                    RoutePrefixes = new[]
                    {
                        "/appointments",
                        "/receptionist/appointments",
                        "/receptionist/working-doctors",
                        "/receptionist/queue-doctors",
                        "/me/appointments",
                        "/admin/appointments",
                        "/admin/statistics"
                    },
                    AuthPolicy = "BearerRequired",
                    AnonymousRoutes = Array.Empty<string>(),
                    AuthenticatedRoutes = new[]
                    {
                        "POST /appointments",
                        "GET /appointments/{appointmentId}",
                        "PATCH /appointments/{appointmentId}",
                        "POST /appointments/{appointmentId}/confirm",
                        "POST /appointments/{appointmentId}/check-in",
                        "POST /appointments/{appointmentId}/cancel",
                        "GET /me/appointments",
                        "GET /patients/{patientId}/appointments",
                        "GET /patients/{patientId}/appointments/upcoming",
                        "GET /receptionist/working-doctors",
                        "GET /receptionist/queue-doctors",
                        "POST /receptionist/appointments/walk-in",
                        "POST /receptionist/appointments",
                        "GET /receptionist/appointments/pending",
                        "PATCH /receptionist/appointments/{appointmentId}",
                        "GET /admin/appointments",
                        "GET /admin/appointments/summary",
                        "GET /admin/appointments/{appointmentId}/history",
                        "GET /admin/statistics"
                    },
                    SpecVisibility = "Internal",
                    OpenApiAccessPolicy = "Admin",
                    OpenApiDocUrl = "/appointment/openapi.json"
                },
                new()
                {
                    Name = "Clinic.Queue.Service",
                    Role = "Daily Queue Ticket Issuance, Room Calling, Real-Time Board, Metrics",
                    Authority = ResolveAuthority("Queue"),
                    AuthorityScope = "InternalClusterOnly",
                    HealthEndpoint = "/readyz",
                    RoutePrefixes = new[] { "/queue", "/doctors/{doctorId}/queue", "/me/queue-status", "/admin/dashboard" },
                    AuthPolicy = "BearerRequired",
                    AnonymousRoutes = Array.Empty<string>(),
                    AuthenticatedRoutes = new[]
                    {
                        "POST /queue/call-next/{doctorId}",
                        "POST /queue/{queueTicketId}/call",
                        "POST /queue/{queueTicketId}/start-exam",
                        "POST /queue/{queueTicketId}/complete-exam",
                        "POST /queue/{queueTicketId}/skip",
                        "POST /queue/{queueTicketId}/retry",
                        "PATCH /queue/{queueTicketId}/priority",
                        "GET /queue/{queueTicketId}",
                        "GET /queue/appointment/{appointmentId}",
                        "GET /doctors/{doctorId}/queue",
                        "POST /doctors/{doctorId}/queue/next",
                        "GET /me/queue-status"
                    },
                    SpecVisibility = "Internal",
                    OpenApiAccessPolicy = "Admin",
                    OpenApiDocUrl = "/queue/openapi.json"
                },
                new()
                {
                    Name = "Clinic.MedicalRecords.Service",
                    Role = "Medical Encounter History, Examination Reports, and Patient Projections",
                    Authority = ResolveAuthority("MedicalRecords"),
                    AuthorityScope = "InternalClusterOnly",
                    HealthEndpoint = "/readyz",
                    RoutePrefixes = new[] { "/patients", "/medical-reports", "/medical-records", "/me/medical-history" },
                    AuthPolicy = "BearerRequired",
                    AnonymousRoutes = Array.Empty<string>(),
                    AuthenticatedRoutes = new[]
                    {
                        "GET /patients",
                        "GET /patients/{patientId}",
                        "GET /patients/me",
                        "PUT /patients/me",
                        "POST /patients",
                        "POST /patients/add",
                        "PUT /patients/{patientId}",
                        "GET /patients/{patientId}/medical-history",
                        "GET /me/medical-history",
                        "GET /medical-reports",
                        "POST /medical-reports",
                        "PUT /medical-reports/{id}",
                        "GET /medical-reports/{id}",
                        "GET /medical-reports/appointment/{appointmentId}",
                        "POST /medical-reports/{id}/finalize",
                        "POST /medical-reports/appointments/{appointmentId}/amendments",
                        "GET /medical-reports/appointments/{appointmentId}/audits",
                        "POST /medical-records/encounters/{appointmentId}/draft",
                        "GET /medical-records/encounters/{appointmentId}",
                        "POST /medical-records/reports/{reportId}/finalize",
                        "POST /medical-records/reports/{reportId}/amendments"
                    },
                    SpecVisibility = "Internal",
                    OpenApiAccessPolicy = "Admin",
                    OpenApiDocUrl = "/medical-records/openapi.json"
                },
                new()
                {
                    Name = "Clinic.Notification.Service",
                    Role = "Email (SMTP), In-App, SignalR, Retry & DLQ; SMS is not implemented",
                    Authority = ResolveAuthority("Notification"),
                    AuthorityScope = "InternalClusterOnly",
                    HealthEndpoint = "/readyz",
                    RoutePrefixes = new[]
                    {
                        "/notifications",
                        "/me/notifications",
                        "/hubs/notifications",
                        "/admin/notifications/dead-lettered",
                        "/admin/notifications/deliveries/{deliveryId}/replay"
                    },
                    AuthPolicy = "BearerRequired",
                    AnonymousRoutes = Array.Empty<string>(),
                    AuthenticatedRoutes = new[]
                    {
                        "GET /notifications",
                        "GET /notifications/unread-count",
                        "POST /notifications/{id}/read",
                        "PUT /notifications/{id}/read",
                        "POST /notifications/read-all",
                        "GET /me/notifications",
                        "GET /admin/notifications/dead-lettered",
                        "POST /admin/notifications/deliveries/{deliveryId}/replay",
                        "GET /hubs/notifications"
                    },
                    SpecVisibility = "Internal",
                    OpenApiAccessPolicy = "Admin",
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

    private string ResolveAuthority(string serviceKey) =>
        _configuration[$"{serviceKey}:Authority"]
        ?? _configuration[$"Services:{serviceKey}:BaseUrl"]
        ?? "unconfigured";
}

public sealed class ApiCatalogResponse
{
    public string GatewayVersion { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string OpenApiSpecUrl { get; set; } = string.Empty;
    public string OpenApiSpecVisibility { get; set; } = "Public";
    public string SwaggerUiUrl { get; set; } = string.Empty;
    public List<ServiceCatalogEntry> Services { get; set; } = new();
    public QoSPolicySummary QoSPolicies { get; set; } = new();
}

public sealed class ServiceCatalogEntry
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public string AuthorityScope { get; set; } = "InternalClusterOnly";
    public string HealthEndpoint { get; set; } = string.Empty;
    public string[] RoutePrefixes { get; set; } = Array.Empty<string>();
    public string AuthPolicy { get; set; } = string.Empty;
    public string[] AnonymousRoutes { get; set; } = Array.Empty<string>();
    public string[] AuthenticatedRoutes { get; set; } = Array.Empty<string>();
    public string SpecVisibility { get; set; } = "Internal";
    public string OpenApiAccessPolicy { get; set; } = "Admin";
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
