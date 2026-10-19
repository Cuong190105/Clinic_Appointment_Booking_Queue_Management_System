using Clinic.IntegrationTests.Fixtures;

namespace Clinic.IntegrationTests.Messaging;

public sealed class TelemetryIntegrationTests : IClassFixture<ApiContractFactory>
{
    private readonly ApiContractFactory _factory;

    public TelemetryIntegrationTests(ApiContractFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Prometheus_endpoint_exposes_scrape_payload()
    {
        using var client = _factory.CreateAnonymousClient();
        using var warmupResponse = await client.GetAsync("/metrics");
        Assert.Equal(HttpStatusCode.OK, warmupResponse.StatusCode);

        using var metricsResponse = await client.GetAsync("/metrics");
        Assert.Equal(HttpStatusCode.OK, metricsResponse.StatusCode);
        var metrics = await metricsResponse.Content.ReadAsStringAsync();
        Assert.Contains("# TYPE target_info gauge", metrics);
    }
}
