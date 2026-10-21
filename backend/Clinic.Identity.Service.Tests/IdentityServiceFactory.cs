using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using Clinic.Identity.Service.Persistence;

namespace Clinic.Identity.Service.Tests;

public sealed class IdentityServiceFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"ClinicIdentityTests_{Environment.ProcessId}_{Guid.NewGuid():N}";
    private readonly string _connectionString;
    private readonly string _keyDirectory = Path.Combine(Path.GetTempPath(), "clinic-identity-tests", Guid.NewGuid().ToString("N"));

    public const string ServiceToken = "integration-test-service-token-at-least-32-bytes";

    public IdentityServiceFactory()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_CONNECTION_STRING")
            ?? "Server=localhost,1433;Database=master;User Id=sa;Password=ClinicDev!2026StrongPw;TrustServerCertificate=True;Encrypt=False";
        var builder = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = _databaseName };
        _connectionString = builder.ConnectionString;
        Environment.SetEnvironmentVariable("ConnectionStrings__IdentityConnection", _connectionString);
        Environment.SetEnvironmentVariable("Identity__ServiceToken", ServiceToken);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "https://identity.test");
        Environment.SetEnvironmentVariable("Jwt__Audience", "Clinic.Services");
        Environment.SetEnvironmentVariable("Jwt__SigningKeyDirectory", _keyDirectory);
        Environment.SetEnvironmentVariable("Jwt__PublicBaseUrl", "https://identity.test");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var serviceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Clinic.Identity.Service"));
        builder.UseContentRoot(serviceRoot);
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:IdentityConnection"] = _connectionString,
            ["Identity:ServiceToken"] = ServiceToken,
            ["Jwt:Issuer"] = "https://identity.test",
            ["Jwt:Audience"] = "Clinic.Services",
            ["Jwt:SigningKeyDirectory"] = _keyDirectory,
            ["Jwt:PublicBaseUrl"] = "https://identity.test"
        }));
    }

    public IdentityDbContext CreateDbContext() => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseSqlServer(_connectionString).Options);

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
        if (Directory.Exists(_keyDirectory)) Directory.Delete(_keyDirectory, recursive: true);
    }
}
