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
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString
            });
        });

        builder.ConfigureTestServices(services =>
        {
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
                new Claim("permission", "medical-report.view.any")
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
