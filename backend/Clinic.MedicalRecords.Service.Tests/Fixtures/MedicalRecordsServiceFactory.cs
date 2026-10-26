using System.Security.Claims;
using System.Text.Encodings.Web;
using Clinic.MedicalRecords.Service.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Clinic.MedicalRecords.Service.Tests.Fixtures;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuth";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey("X-Test-NoAuth"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>();

        if (Request.Headers.TryGetValue("X-Test-Role", out var roleVal))
        {
            claims.Add(new Claim(ClaimTypes.Role, roleVal!));
        }

        if (Request.Headers.TryGetValue("X-Test-Permissions", out var permVal))
        {
            foreach (var perm in permVal.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim("permission", perm));
            }
        }

        if (Request.Headers.TryGetValue("X-Test-PatientId", out var patVal))
        {
            claims.Add(new Claim("patientId", patVal!));
        }

        if (Request.Headers.TryGetValue("X-Test-DoctorId", out var docVal))
        {
            claims.Add(new Claim("doctorId", docVal!));
        }

        if (Request.Headers.TryGetValue("X-Test-Sub", out var subVal))
        {
            claims.Add(new Claim("sub", subVal!));
        }

        // If no explicit auth headers are specified, default to Admin with all permissions
        if (claims.Count == 0)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            claims.Add(new Claim(ClaimTypes.Role, "Receptionist"));
            claims.Add(new Claim(ClaimTypes.Role, "Doctor"));
            claims.Add(new Claim(ClaimTypes.Role, "Patient"));
            claims.Add(new Claim("sub", Guid.NewGuid().ToString()));
            claims.Add(new Claim("permission", "patient.create.any"));
            claims.Add(new Claim("permission", "patient.view.any"));
            claims.Add(new Claim("permission", "patient.edit.any"));
            claims.Add(new Claim("permission", "patient.view.own"));
            claims.Add(new Claim("permission", "patient.edit.own"));
            claims.Add(new Claim("permission", "medical-report.create"));
            claims.Add(new Claim("permission", "medical-report.view.own"));
            claims.Add(new Claim("permission", "medical-report.view.related"));
            claims.Add(new Claim("permission", "medical-report.view.any"));
            claims.Add(new Claim("permission", "medical-report.edit.own"));
            claims.Add(new Claim("permission", "medical-report.edit.any"));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class MedicalRecordsServiceFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MedicalRecordsConnection"] = "",
            ["Identity:Authority"] = "http://localhost:5001",
            ["Jwt:Issuer"] = "http://localhost:5000",
            ["Jwt:Audience"] = "Clinic.Services",
            ["Identity:ServiceToken"] = "integration-test-service-token"
        }));

        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<MedicalRecordsDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<MedicalRecordsDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
