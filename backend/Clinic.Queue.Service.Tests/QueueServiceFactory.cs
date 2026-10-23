using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Clinic.Queue.Service.Tests;

public sealed class QueueServiceFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:QueueConnection"] = "Server=localhost,1433;Database=ClinicQueueDB;User Id=sa;Password=ClinicDev!2026StrongPw;TrustServerCertificate=True;Encrypt=False",
            ["Identity:Authority"] = "http://localhost:5001",
            ["Jwt:Issuer"] = "http://localhost:5000",
            ["Jwt:Audience"] = "Clinic.Services"
        }));
    }
}
