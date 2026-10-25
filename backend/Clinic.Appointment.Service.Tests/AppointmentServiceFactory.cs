using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Clinic.Appointment.Service.Tests;

public sealed class AppointmentServiceFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:AppointmentConnection"] = "Server=localhost,1433;Database=ClinicAppointmentDB;User Id=sa;Password=ClinicDev!2026StrongPw;TrustServerCertificate=True;Encrypt=False",
            ["Identity:Authority"] = "http://localhost:5001",
            ["Jwt:Issuer"] = "http://localhost:5000",
            ["Jwt:Audience"] = "Clinic.Services",
            ["Identity:ServiceToken"] = "integration-test-service-token",
            ["Services:Scheduling:BaseUrl"] = "http://localhost:5002"
        }));
    }
}

