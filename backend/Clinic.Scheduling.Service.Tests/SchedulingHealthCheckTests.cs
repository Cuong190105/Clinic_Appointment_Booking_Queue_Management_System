using System.Net;
using System.Text.Json;

namespace Clinic.Scheduling.Service.Tests;

public sealed class SchedulingHealthCheckTests : IClassFixture<SchedulingServiceFactory>
{
    private readonly HttpClient _client;

    public SchedulingHealthCheckTests(SchedulingServiceFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Healthz_ReturnsOk_WithHealthyStatus()
    {
        var response = await _client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Clinic.Scheduling.Service", json.RootElement.GetProperty("service").GetString());
    }

    [Fact]
    public async Task Readyz_ReturnsOk_WithReadyStatus()
    {
        var response = await _client.GetAsync("/readyz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        Assert.Equal("Ready", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task RootEndpoint_ReturnsOk_WithServiceInfo()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        Assert.Equal("Clinic.Scheduling.Service", json.RootElement.GetProperty("service").GetString());
        Assert.Equal("1.0.0", json.RootElement.GetProperty("version").GetString());
    }
}

