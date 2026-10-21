using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Clinic.Scheduling.Service.Tests;

public sealed class SchedulingServiceFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SchedulingConnection"] = "Server=localhost,1433;Database=ClinicSchedulingDB;User Id=sa;Password=ClinicDev!2026StrongPw;TrustServerCertificate=True;Encrypt=False",
            ["Identity:Authority"] = "http://localhost:5001",
            ["Jwt:Issuer"] = "http://localhost:5000",
            ["Jwt:Audience"] = "Clinic.Services"
        }));
    }
}

