using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Clinic.IntegrationTests.Fixtures;

public sealed class ApiContractFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SkipDatabaseInitialization"] = "true",
                ["SkipBackgroundWorkers"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
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

    public HttpClient CreateAuthenticatedClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });
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
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "integration-test-user") },
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
