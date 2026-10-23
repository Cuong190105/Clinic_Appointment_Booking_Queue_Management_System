using System.Net;
using System.Text.Json;
using Xunit;

namespace Clinic.Queue.Service.Tests;

public class QueueServiceHealthTests : IClassFixture<QueueServiceFactory>
{
    private readonly HttpClient _client;

    public QueueServiceHealthTests(QueueServiceFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Healthz_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Clinic.Queue.Service", json.RootElement.GetProperty("service").GetString());
    }

    [Fact]
    public async Task Readyz_ReturnsReady()
    {
        var response = await _client.GetAsync("/readyz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        Assert.Equal("Ready", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Root_ReturnsServiceInfo()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        Assert.Equal("Clinic.Queue.Service", json.RootElement.GetProperty("service").GetString());
        Assert.Equal("1.0.0", json.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

