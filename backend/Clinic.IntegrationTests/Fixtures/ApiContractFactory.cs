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
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Clinic.IntegrationTests.Fixtures;

public sealed class ApiContractFactory : WebApplicationFactory<Program>
{
    public static readonly Guid PatientUserId = Guid.Parse("a0000002-0000-0000-0000-000000000023");
    public static readonly Guid DoctorUserId = Guid.Parse("a0000002-0000-0000-0000-000000000001");
    public static readonly Guid PatientId = Guid.Parse("a0000005-0000-0000-0000-000000000001");
    public static readonly Guid DoctorId = Guid.Parse("a0000004-0000-0000-0000-000000000001");

    private readonly string _databaseName = $"ClinicBaselineTests_{Environment.ProcessId}_{Guid.NewGuid():N}";
    private string? _testConnectionString;
    private bool _databaseInitialized;

    public ApiContractFactory()
    {
        var appSettingsPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Clinic.API", "appsettings.json"));
        var sourceConnectionString = Environment.GetEnvironmentVariable("CLINIC_BASELINE_TEST_CONNECTION_STRING")
            ?? new ConfigurationBuilder()
                .AddJsonFile(appSettingsPath, optional: false)
                .Build()
                .GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is required for SQL Server baseline tests.");

        var connectionBuilder = new DbConnectionStringBuilder { ConnectionString = sourceConnectionString };
        connectionBuilder.Remove("Initial Catalog");
        connectionBuilder["Database"] = _databaseName;
        _testConnectionString = connectionBuilder.ConnectionString;
    }

    public string TestConnectionString => _testConnectionString
        ?? throw new InvalidOperationException("The baseline test connection has not been configured.");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SkipDatabaseInitialization"] = "true",
                ["SkipBackgroundWorkers"] = "true",
                ["Identity:ServiceToken"] = "integration-test-service-token",
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory, TestIdentityHttpClientFactory>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(_testConnectionString!));

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

    public async Task InitializeDatabaseAsync()
    {
        if (_databaseInitialized)
            return;

        await using var dbContext = CreateDbContext();
        if (!string.Equals(dbContext.Database.GetDbConnection().Database, _databaseName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Baseline tests resolved a database other than their dedicated test database.");
        }

        await dbContext.Database.MigrateAsync();

        _databaseInitialized = true;
    }

    public ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(TestConnectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    public async Task DeleteTestDatabaseAsync()
    {
        if (!_databaseInitialized)
            return;

        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await DeleteTestDatabaseAsync();
        await base.DisposeAsync();
    }
}

public sealed class TestIdentityHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(new TestIdentityHandler())
    {
        BaseAddress = new Uri("http://identity.test")
    };

    private sealed class TestIdentityHandler : HttpMessageHandler
    {
        private readonly HashSet<string> _reservedSlots = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
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
                    scheduleDate = new Clinic.Domain.Common.TimeConverter().Today.AddDays(2).ToString("yyyy-MM-dd")
                });
                return Json(HttpStatusCode.OK, res);
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
        var userId = Request.Headers["X-Test-User-Id"].FirstOrDefault()
            ?? ApiContractFactory.PatientUserId.ToString();
        var doctorId = Request.Headers["X-Test-Doctor-Id"].FirstOrDefault()
            ?? ApiContractFactory.DoctorId.ToString();
        var patientId = Request.Headers["X-Test-Patient-Id"].FirstOrDefault()
            ?? ApiContractFactory.PatientId.ToString();

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("doctorId", doctorId),
                new Claim("patientId", patientId),
                new Claim("permission", "queue.start-exam.any"),
                new Claim("permission", "queue.complete-exam.any"),
                new Claim("permission", "medical-report.view.any"),
                new Claim("permission", "user.manage"),
                new Claim("permission", "employee.create"),
                new Claim("permission", "doctor.create"),
                new Claim("permission", "role.manage")
            },
            Scheme);
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
