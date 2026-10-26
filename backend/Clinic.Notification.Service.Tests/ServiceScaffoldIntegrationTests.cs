using Microsoft.AspNetCore.Mvc.Testing;

namespace Clinic.Notification.Service.Tests;

public sealed class ServiceScaffoldIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ServiceScaffoldIntegrationTests(WebApplicationFactory<Program> factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Healthz_ReturnsSuccess()
    {
        using var response = await _client.GetAsync("/healthz");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readyz_ReturnsSuccess_ForScaffoldWithoutExternalDependencies()
    {
        using var response = await _client.GetAsync("/readyz");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
