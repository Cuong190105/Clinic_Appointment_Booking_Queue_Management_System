using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Clinic.MedicalRecords.Service.Entities;
using Clinic.MedicalRecords.Service.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Clinic.MedicalRecords.Service.Tests;

public sealed class MedicalRecordsFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MedicalRecordsConnection"] = "",
            ["SkipBackgroundWorkers"] = "true",
            ["SkipDatabaseInitialization"] = "true"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<MedicalRecordsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<MedicalRecordsDbContext>>();
            services.RemoveAll<MedicalRecordsDbContext>();
            services.AddDbContext<MedicalRecordsDbContext>(o => o.UseInMemoryDatabase(databaseName));
            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = MedicalTestAuthHandler.SchemeName;
                o.DefaultChallengeScheme = MedicalTestAuthHandler.SchemeName;
                o.DefaultForbidScheme = MedicalTestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, MedicalTestAuthHandler>(MedicalTestAuthHandler.SchemeName, _ => { });
            services.AddHttpClient("Queue").ConfigurePrimaryHttpMessageHandler(() => new QueueNotFoundHandler());
        });
    }

    private sealed class QueueNotFoundHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

public sealed class MedicalTestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "MedicalTest";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var patientId = Request.Headers.TryGetValue("X-Test-PatientId", out var value) ? value.ToString() : Guid.NewGuid().ToString();
        var claims = new[] { new Claim("sub", patientId), new Claim("patientId", patientId), new Claim(ClaimTypes.Role, "Admin") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

public sealed class MedicalRecordsContractTests(MedicalRecordsFactory factory) : IClassFixture<MedicalRecordsFactory>
{
    [Fact]
    public async Task SaveAndFinalizeContract_PersistsHistoryAndMakesReportImmutable()
    {
        using var client = factory.CreateClient();
        var appointmentId = Guid.NewGuid(); var patientId = Guid.NewGuid();
        using var create = await client.PostAsJsonAsync("/medical-reports", new
        {
            appointmentId, patientId, patientName = "Patient", doctorId = Guid.NewGuid(), doctorName = "Doctor",
            specialtyName = "General", examDate = "2026-11-02", symptoms = "symptoms", isFinalize = false
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var draft = await create.Content.ReadFromJsonAsync<MedicalRecord>();
        Assert.NotNull(draft); Assert.Equal(MedicalRecordStatus.Draft, draft.Status);

        using var finalize = await client.PutAsJsonAsync($"/medical-reports/{draft.Id}", new
        {
            symptoms = "symptoms", diagnosis = "diagnosis", prescription = "prescription", notes = "notes", isFinalize = true
        });
        Assert.Equal(HttpStatusCode.OK, finalize.StatusCode);
        var finalized = await finalize.Content.ReadFromJsonAsync<MedicalRecord>();
        Assert.NotNull(finalized); Assert.Equal(MedicalRecordStatus.Finalized, finalized.Status);

        using var mutate = await client.PutAsJsonAsync($"/medical-reports/{draft.Id}", new { diagnosis = "changed", isFinalize = false });
        Assert.Equal(HttpStatusCode.Conflict, mutate.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MedicalRecordsDbContext>();
        var history = Assert.Single(await db.MedicalHistory.AsNoTracking().Where(x => x.PatientId == patientId).ToListAsync());
        Assert.Equal("diagnosis", history.Diagnosis); Assert.Equal(2, history.SourceVersion);
    }
}
