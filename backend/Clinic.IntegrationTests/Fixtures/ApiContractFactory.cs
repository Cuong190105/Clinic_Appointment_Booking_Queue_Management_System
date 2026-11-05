extern alias IdentityServiceApp;
extern alias SchedulingServiceApp;
extern alias AppointmentServiceApp;
extern alias QueueServiceApp;
extern alias MedicalRecordsServiceApp;
extern alias NotificationServiceApp;

using System.Net;
using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Clinic.IntegrationTests.Fixtures;

public sealed class ApiContractFactory : WebApplicationFactory<Program>
{
    public static readonly Guid PatientUserId = Guid.Parse("a0000002-0000-0000-0000-000000000023");
    public static readonly Guid DoctorUserId = Guid.Parse("a0000002-0000-0000-0000-000000000001");
    public static readonly Guid PatientId = Guid.Parse("a0000005-0000-0000-0000-000000000001");
    public static readonly Guid DoctorId = Guid.Parse("a0000004-0000-0000-0000-000000000001");

    public ApiContractFactory()
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:ServiceToken"] = "integration-test-service-token",
                ["Services:InternalToken"] = "integration-test-service-token",
                ["Identity:Authority"] = "http://clinic-identity-service:8080",
                ["Scheduling:Authority"] = "http://clinic-scheduling-service:8080",
                ["Appointment:Authority"] = "http://clinic-appointment-service:8080",
                ["Queue:Authority"] = "http://clinic-queue-service:8080",
                ["MedicalRecords:Authority"] = "http://clinic-medicalrecords-service:8080",
                ["Notification:Authority"] = "http://clinic-notification-service:8080"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory, TestIdentityHttpClientFactory>();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.Scheme;
                options.DefaultChallengeScheme = TestAuthenticationHandler.Scheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.Scheme, _ => { });
        });
    }

    public HttpClient CreateAnonymousClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    public HttpClient CreateStrictlyAnonymousClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
        return client;
    }

    public HttpClient CreateRoleClient(string role, Guid? userId = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User-Id", (userId ?? PatientUserId).ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }

    public HttpClient CreateAuthenticatedClient(
        Guid? userId = null,
        Guid? doctorId = null,
        Guid? patientId = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User-Id", (userId ?? PatientUserId).ToString());
        client.DefaultRequestHeaders.Add("X-Test-Doctor-Id", (doctorId ?? DoctorId).ToString());
        client.DefaultRequestHeaders.Add("X-Test-Patient-Id", (patientId ?? PatientId).ToString());
        return client;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
    }
}

/// <summary>
/// Boots real in-process WebApplicationFactory hosts for all 6 microservices so contract/drift tests
/// fetch real runtime OpenAPI documents rather than hardcoded mock JSON.
/// </summary>
public static class RealMicroserviceOpenApiRegistry
{
    public const string InternalServiceToken = "integration-test-service-token";

    private static readonly object SyncLock = new();
    private static WebApplicationFactory<IdentityServiceApp::Program>? _identityHost;
    private static WebApplicationFactory<SchedulingServiceApp::Program>? _schedulingHost;
    private static WebApplicationFactory<AppointmentServiceApp::Program>? _appointmentHost;
    private static WebApplicationFactory<QueueServiceApp::Program>? _queueHost;
    private static WebApplicationFactory<MedicalRecordsServiceApp::Program>? _medicalRecordsHost;
    private static WebApplicationFactory<NotificationServiceApp::Program>? _notificationHost;

    private static WebApplicationFactory<IdentityServiceApp::Program> GetIdentityHost()
    {
        lock (SyncLock)
        {
            return _identityHost ??= new WebApplicationFactory<IdentityServiceApp::Program>().WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Testing");
                b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SkipDatabaseInitialization"] = "true",
                    ["SkipBackgroundWorkers"] = "true",
                    ["Identity:ServiceToken"] = InternalServiceToken,
                    ["Services:InternalToken"] = InternalServiceToken,
                    ["ConnectionStrings:IdentityConnection"] = "Server=(localdb)\\mssqllocaldb;Database=IdentityOpenApiSpec;Trusted_Connection=True;TrustServerCertificate=True;"
                }));
            });
        }
    }

    private static WebApplicationFactory<SchedulingServiceApp::Program> GetSchedulingHost()
    {
        lock (SyncLock)
        {
            return _schedulingHost ??= new WebApplicationFactory<SchedulingServiceApp::Program>().WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Testing");
                b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SkipDatabaseInitialization"] = "true",
                    ["SkipBackgroundWorkers"] = "true",
                    ["Identity:ServiceToken"] = InternalServiceToken,
                    ["Services:InternalToken"] = InternalServiceToken
                }));
            });
        }
    }

    private static WebApplicationFactory<AppointmentServiceApp::Program> GetAppointmentHost()
    {
        lock (SyncLock)
        {
            return _appointmentHost ??= new WebApplicationFactory<AppointmentServiceApp::Program>().WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Testing");
                b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SkipDatabaseInitialization"] = "true",
                    ["SkipBackgroundWorkers"] = "true",
                    ["Identity:ServiceToken"] = InternalServiceToken,
                    ["Services:InternalToken"] = InternalServiceToken
                }));
            });
        }
    }

    private static WebApplicationFactory<QueueServiceApp::Program> GetQueueHost()
    {
        lock (SyncLock)
        {
            return _queueHost ??= new WebApplicationFactory<QueueServiceApp::Program>().WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Testing");
                b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SkipDatabaseInitialization"] = "true",
                    ["SkipBackgroundWorkers"] = "true",
                    ["Identity:ServiceToken"] = InternalServiceToken,
                    ["Services:InternalToken"] = InternalServiceToken
                }));
            });
        }
    }

    private static WebApplicationFactory<MedicalRecordsServiceApp::Program> GetMedicalRecordsHost()
    {
        lock (SyncLock)
        {
            return _medicalRecordsHost ??= new WebApplicationFactory<MedicalRecordsServiceApp::Program>().WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Testing");
                b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SkipDatabaseInitialization"] = "true",
                    ["SkipBackgroundWorkers"] = "true",
                    ["Identity:ServiceToken"] = InternalServiceToken,
                    ["Services:InternalToken"] = InternalServiceToken
                }));
            });
        }
    }

    private static WebApplicationFactory<NotificationServiceApp::Program> GetNotificationHost()
    {
        lock (SyncLock)
        {
            return _notificationHost ??= new WebApplicationFactory<NotificationServiceApp::Program>().WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Testing");
                b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SkipDatabaseInitialization"] = "true",
                    ["SkipBackgroundWorkers"] = "true",
                    ["Identity:ServiceToken"] = InternalServiceToken,
                    ["Services:InternalToken"] = InternalServiceToken
                }));
            });
        }
    }

    public static HttpClient CreateAnonymousServiceClient(string serviceName) => serviceName.ToLowerInvariant() switch
    {
        "identity" => GetIdentityHost().CreateClient(),
        "scheduling" => GetSchedulingHost().CreateClient(),
        "appointment" => GetAppointmentHost().CreateClient(),
        "queue" => GetQueueHost().CreateClient(),
        "medicalrecords" or "medical-records" or "medical records" => GetMedicalRecordsHost().CreateClient(),
        "notification" => GetNotificationHost().CreateClient(),
        _ => throw new ArgumentOutOfRangeException(nameof(serviceName), $"Unknown microservice '{serviceName}'.")
    };

    public static HttpClient CreateServiceClient(string serviceName)
    {
        var client = CreateAnonymousServiceClient(serviceName);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Clinic-Service-Token", InternalServiceToken);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Identity-Service-Token", InternalServiceToken);
        return client;
    }

    public static async ValueTask DisposeAllAsync()
    {
        IAsyncDisposable?[] hosts;
        lock (SyncLock)
        {
            hosts =
            [
                _identityHost,
                _schedulingHost,
                _appointmentHost,
                _queueHost,
                _medicalRecordsHost,
                _notificationHost
            ];
            _identityHost = null;
            _schedulingHost = null;
            _appointmentHost = null;
            _queueHost = null;
            _medicalRecordsHost = null;
            _notificationHost = null;
        }

        foreach (var host in hosts)
        {
            if (host is not null)
            {
                try
                {
                    await host.DisposeAsync();
                }
                catch
                {
                    // Ignore shutdown errors during test cleanup
                }
            }
        }
    }
}

public sealed class TestIdentityHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(new TestIdentityHandler(name))
    {
        BaseAddress = new Uri("http://identity.test")
    };

    private sealed class TestIdentityHandler(string clientName) : HttpMessageHandler
    {
        private static readonly HashSet<string> _reservedSlots = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Equals("/employees", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                var contentStr = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : null;
                string fullName = "Identity Employee Test";
                Guid employeeId = Guid.NewGuid();
                if (!string.IsNullOrWhiteSpace(contentStr))
                {
                    using var body = JsonDocument.Parse(contentStr);
                    var root = body.RootElement;
                    if (root.TryGetProperty("id", out var requestedId) && requestedId.ValueKind == JsonValueKind.String
                        && Guid.TryParse(requestedId.GetString(), out var parsedId) && parsedId != Guid.Empty)
                    {
                        employeeId = parsedId;
                    }
                    if (root.TryGetProperty("fullName", out var fnProp) && fnProp.ValueKind == JsonValueKind.String)
                    {
                        fullName = fnProp.GetString() ?? fullName;
                    }
                }
                return Json(HttpStatusCode.Created, JsonSerializer.Serialize(new { id = employeeId, fullName = fullName }));
            }
            if (path.Contains("/doctors/dashboard-stats", StringComparison.OrdinalIgnoreCase))
            {
                return Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { totalDoctors = 5, totalDoctorsChange = 0.0, activeDoctors = 5, activeDoctorsChange = 0.0 }));
            }
            if (path.Equals("/shifts/suggestions", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                return Json(HttpStatusCode.Created, JsonSerializer.Serialize(new { result = new { id = Guid.NewGuid() } }));
            }
            if (path.StartsWith("/shifts/suggestions/", StringComparison.OrdinalIgnoreCase)
                && path.EndsWith("/approve", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (path.Contains("/appointments/") && path.EndsWith("/check-in", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.Equals("/appointments", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                var bodyText = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
                var timeSlot = "09:00:00";
                var schedId = Guid.Empty;
                if (!string.IsNullOrEmpty(bodyText))
                {
                    using var body = JsonDocument.Parse(bodyText);
                    var root = body.RootElement;
                    if (root.TryGetProperty("timeSlot", out var ts) || root.TryGetProperty("TimeSlot", out ts)) timeSlot = ts.GetString() ?? "09:00:00";
                    if (root.TryGetProperty("workScheduleId", out var ws) || root.TryGetProperty("WorkScheduleId", out ws)) schedId = ws.GetGuid();
                }
                var slotKey = $"app_{schedId}_{timeSlot}";
                if (_reservedSlots.Contains(slotKey))
                {
                    return new HttpResponseMessage(HttpStatusCode.Conflict)
                    {
                        Content = new StringContent("{\"error\":\"Slot already booked\"}", System.Text.Encoding.UTF8, "application/json")
                    };
                }
                _reservedSlots.Add(slotKey);
                var appId = Guid.NewGuid();
                var result = JsonSerializer.Serialize(new
                {
                    result = new
                    {
                        id = appId,
                        patientId = ApiContractFactory.PatientId,
                        patientName = "Patient Name",
                        doctorId = ApiContractFactory.DoctorId,
                        doctorName = "Doctor Name",
                        workScheduleId = schedId,
                        specialtyName = "Cardiology",
                        date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2).ToString("yyyy-MM-dd"),
                        timeSlot = timeSlot,
                        status = "Pending",
                        reason = "baseline booking"
                    }
                });
                return Json(HttpStatusCode.Created, result);
            }
            if (path.Contains("/reservations", StringComparison.OrdinalIgnoreCase))
            {
                if (path.EndsWith("/confirm", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/release", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }
                
                var bodyText = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
                var timeSlot = "09:00:00";
                if (!string.IsNullOrEmpty(bodyText))
                {
                    using var body = JsonDocument.Parse(bodyText);
                    if (body.RootElement.TryGetProperty("timeSlot", out var ts))
                    {
                        timeSlot = ts.GetString() ?? "09:00:00";
                    }
                }
                var slotKey = $"{request.RequestUri?.AbsolutePath}_{timeSlot}";
                if (_reservedSlots.Contains(slotKey))
                {
                    return new HttpResponseMessage(HttpStatusCode.Conflict)
                    {
                        Content = new StringContent("{\"error\":\"Slot already reserved\"}", System.Text.Encoding.UTF8, "application/json")
                    };
                }
                _reservedSlots.Add(slotKey);

                var reservationId = Guid.NewGuid();
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var schedIdx = Array.FindIndex(segments, s => s.Equals("schedules", StringComparison.OrdinalIgnoreCase));
                var scheduleId = Guid.NewGuid();
                if (schedIdx != -1 && schedIdx + 1 < segments.Length && Guid.TryParse(segments[schedIdx + 1], out var parsedSchedId))
                {
                    scheduleId = parsedSchedId;
                }

                var res = JsonSerializer.Serialize(new
                {
                    id = reservationId,
                    scheduleId = scheduleId,
                    timeSlot = timeSlot,
                    patientId = ApiContractFactory.PatientId,
                    status = "Reserved",
                    expiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                    idempotencyKey = Guid.NewGuid().ToString("N"),
                    doctorId = ApiContractFactory.DoctorId,
                    doctorName = "Doctor Name",
                    specialtyName = "Cardiology",
                    scheduleDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2).ToString("yyyy-MM-dd")
                });
                return Json(HttpStatusCode.OK, res);
            }

            if (path.Contains("/queue/call-next/", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                var ticketJson = JsonSerializer.Serialize(new
                {
                    result = new
                    {
                        id = Guid.NewGuid(),
                        appointmentId = Guid.NewGuid(),
                        queueNumber = 1,
                        status = "Called",
                        checkInTime = DateTime.UtcNow,
                        callTime = DateTime.UtcNow
                    }
                });
                return Json(HttpStatusCode.OK, ticketJson);
            }

            if (path.Contains("/queue/") && path.EndsWith("/start-exam", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.Contains("/queue/") && path.EndsWith("/complete-exam", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.Contains("/queue/") && path.EndsWith("/skip", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                return Json(HttpStatusCode.OK, "{\"result\":{\"hasAutoCalledNext\":false}}");
            }

            if (path.Contains("/queue/") && path.EndsWith("/priority", StringComparison.OrdinalIgnoreCase) && (request.Method == HttpMethod.Patch || request.Method.Method == "PATCH"))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.Contains("/doctors/") && path.EndsWith("/queue/next", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.Contains("/doctors/") && path.EndsWith("/queue", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Get)
            {
                return Json(HttpStatusCode.OK, "{\"result\":[]}");
            }

            if (path.EndsWith("/me/queue-status", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.EndsWith("/v1/internal/users/provision", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                var root = body.RootElement;
                var provisioned = JsonSerializer.Serialize(new
                {
                    userId = root.GetProperty("userId").GetGuid(),
                    personId = root.GetProperty("personId").GetGuid(),
                    isActive = false,
                    created = true
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(provisioned, System.Text.Encoding.UTF8, "application/json")
                };
            }
            if (path.Contains("/v1/internal/users/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/roles", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Put)
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            if (path.EndsWith("/v1/auth/login", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
                return Json(HttpStatusCode.OK, "{\"accessToken\":\"identity-access-token\",\"refreshToken\":\"identity-refresh-token\",\"roles\":[\"Patient\"]}");
            if (path.EndsWith("/v1/auth/refresh", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            if (path.EndsWith("/v1/auth/revoke", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            if (path.EndsWith("/v1/auth/me", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK, "{\"userId\":\"b0000001-0000-0000-0000-000000000001\",\"personId\":\"b0000002-0000-0000-0000-000000000001\",\"username\":\"identity-admin-user\",\"fullName\":\"Identity Admin User\",\"phoneNumber\":\"09000000000\",\"email\":\"admin-user@example.test\",\"gender\":\"Other\",\"address\":\"Address\",\"dateOfBirth\":\"1990-01-01\",\"roles\":[\"Patient\"],\"isActive\":true,\"createdAt\":\"2026-01-01T00:00:00Z\"}");
            if (path.EndsWith("/v1/auth/forgot-password", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
                return Json(HttpStatusCode.OK, "{\"found\":false,\"personId\":null,\"resetToken\":null}");
            if (path.Equals("/specialties", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK, "{\"isSuccess\":true,\"result\":{\"items\":[{\"id\":\"c0000001-0000-0000-0000-000000000001\",\"name\":\"Cardiology\",\"description\":\"Heart care\",\"establishedDate\":\"2020-01-01\"}],\"pageNumber\":1,\"pageSize\":20,\"totalCount\":1,\"totalPages\":1,\"hasPrevious\":false,\"hasNext\":false}}");
            if (path.StartsWith("/specialties/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/status", StringComparison.OrdinalIgnoreCase) && request.Method.Method == "PATCH")
                return path.Contains(Guid.Empty.ToString(), StringComparison.OrdinalIgnoreCase)
                    ? Json(HttpStatusCode.NotFound, "{\"error\":\"Specialty not found\"}")
                    : new HttpResponseMessage(HttpStatusCode.NoContent);
            if (path.Equals("/doctors", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK, "{\"isSuccess\":true,\"result\":{\"items\":[{\"id\":\"a0000004-0000-0000-0000-000000000001\",\"fullName\":\"Dr. Cardiology\",\"phoneNumber\":\"0901234567\",\"email\":\"doc@clinic.test\",\"licenseNumber\":\"LIC-001\",\"qualification\":\"MD\",\"experienceYears\":10,\"biography\":\"Senior Doctor\",\"status\":\"Active\",\"currentSpecialty\":\"Cardiology\"}],\"pageNumber\":1,\"pageSize\":10,\"totalCount\":1,\"totalPages\":1,\"hasPrevious\":false,\"hasNext\":false}}");
            if (path.Equals("/doctors/available", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK, "[{\"id\":\"a0000004-0000-0000-0000-000000000001\",\"fullName\":\"Dr. Cardiology\",\"qualification\":\"MD\",\"experienceYears\":10,\"specialtyId\":\"c0000001-0000-0000-0000-000000000001\"}]");
            if (path.Equals("/schedules/availability", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK, "{\"isSuccess\":true,\"result\":[{\"scheduleId\":\"d0000001-0000-0000-0000-000000000001\",\"doctorId\":\"a0000004-0000-0000-0000-000000000001\",\"doctorName\":\"Dr. Cardiology\",\"specialtyName\":\"Cardiology\",\"date\":\"2026-11-10\",\"timeSlot\":\"09:00:00\",\"shiftStart\":\"08:00:00\",\"shiftEnd\":\"12:00:00\",\"recommendedCapacity\":10,\"remainingCapacity\":8}],\"totalCount\":1}");
            if (path.Equals("/patients/me", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK, "{\"id\":\"a0000005-0000-0000-0000-000000000001\",\"fullName\":\"Patient Name\",\"phoneNumber\":\"0909999999\",\"email\":\"patient@clinic.test\",\"gender\":\"Female\",\"dateOfBirth\":\"1995-05-15\",\"address\":\"HCMC\"}");
            if (path.Equals("/notifications/unread-count", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK, "{\"unreadCount\":2}");
            if (path.EndsWith("openapi/v1.json", StringComparison.OrdinalIgnoreCase))
            {
                using var serviceClient = RealMicroserviceOpenApiRegistry.CreateAnonymousServiceClient(clientName);
                using var forwardReq = new HttpRequestMessage(HttpMethod.Get, "/openapi/v1.json");
                if (request.Headers.TryGetValues("X-Clinic-Service-Token", out var cst))
                    forwardReq.Headers.TryAddWithoutValidation("X-Clinic-Service-Token", cst);
                if (request.Headers.TryGetValues("X-Identity-Service-Token", out var ist))
                    forwardReq.Headers.TryAddWithoutValidation("X-Identity-Service-Token", ist);
                if (request.Headers.TryGetValues("Authorization", out var auth))
                    forwardReq.Headers.TryAddWithoutValidation("Authorization", auth);
                var realSpecResponse = await serviceClient.SendAsync(forwardReq, cancellationToken);
                var specContent = await realSpecResponse.Content.ReadAsStringAsync(cancellationToken);
                return Json(realSpecResponse.StatusCode, specContent);
            }
            var content = path.EndsWith("jwks.json", StringComparison.OrdinalIgnoreCase)
                ? "{\"keys\":[]}"
                : path.EndsWith("/v1/internal/roles", StringComparison.OrdinalIgnoreCase)
                    ? "[{\"id\":\"b0000003-0000-0000-0000-000000000001\",\"name\":\"Patient\",\"description\":\"Patient role\",\"permissions\":[]}]"
                : path.EndsWith("/v1/internal/users", StringComparison.OrdinalIgnoreCase)
                    ? "{\"items\":[{\"userId\":\"b0000001-0000-0000-0000-000000000001\",\"personId\":\"b0000002-0000-0000-0000-000000000001\",\"username\":\"identity-admin-user\",\"fullName\":\"Identity Admin User\",\"phoneNumber\":\"09000000000\",\"email\":\"admin-user@example.test\",\"gender\":\"Other\",\"roles\":[\"Patient\"],\"isActive\":true,\"createdAt\":\"2026-01-01T00:00:00Z\"}],\"totalCount\":1,\"pageNumber\":1,\"pageSize\":10}"
                    : path.Contains("/v1/internal/users/", StringComparison.OrdinalIgnoreCase)
                        ? "{\"userId\":\"b0000001-0000-0000-0000-000000000001\",\"personId\":\"b0000002-0000-0000-0000-000000000001\",\"username\":\"identity-admin-user\",\"fullName\":\"Identity Admin User\",\"phoneNumber\":\"09000000000\",\"email\":\"admin-user@example.test\",\"gender\":\"Other\",\"address\":\"Address\",\"dateOfBirth\":\"1990-01-01\",\"roles\":[\"Patient\"],\"isActive\":true,\"createdAt\":\"2026-01-01T00:00:00Z\",\"patientId\":null,\"employeeId\":null,\"doctorId\":null}"
                        : "{}";
            return Json(HttpStatusCode.OK, content);
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string content) => new(status)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
    }
}

public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public new const string Scheme = "IntegrationTest";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (string.Equals(Request.Headers["X-Test-Anonymous"].FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers["X-Test-User-Id"].FirstOrDefault()
            ?? ApiContractFactory.PatientUserId.ToString();
        var doctorId = Request.Headers["X-Test-Doctor-Id"].FirstOrDefault()
            ?? ApiContractFactory.DoctorId.ToString();
        var patientId = Request.Headers["X-Test-Patient-Id"].FirstOrDefault()
            ?? ApiContractFactory.PatientId.ToString();
        var role = Request.Headers["X-Test-Role"].FirstOrDefault() ?? "Admin";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role),
            new("doctorId", doctorId),
            new("patientId", patientId)
        };

        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            claims.AddRange(
            [
                new Claim("permission", "queue.start-exam.any"),
                new Claim("permission", "queue.complete-exam.any"),
                new Claim("permission", "medical-report.view.any"),
                new Claim("permission", "user.manage"),
                new Claim("permission", "employee.create"),
                new Claim("permission", "doctor.create"),
                new Claim("permission", "role.manage"),
                new Claim("permission", "queue.check-in"),
                new Claim("permission", "appointment.create")
            ]);
        }

        var identity = new ClaimsIdentity(claims, Scheme);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}

public static class ApiResponseAssertions
{
    public static async Task<JsonDocument> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content));
        return JsonDocument.Parse(content);
    }

    public static JsonElement Result(JsonDocument envelope)
    {
        Assert.True(envelope.RootElement.TryGetProperty("result", out var result));
        return result;
    }
}
