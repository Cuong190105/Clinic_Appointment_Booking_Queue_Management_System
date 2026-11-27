using System.Net;
using System.Net.Http.Json;

namespace Clinic.Identity.Service.Tests;

/// <summary>
/// Identity's auth limiter must partition per real client (SLI_SLO_MATRIX §5.3): behind the Gateway
/// every request arrives from the Gateway's IP, so the forwarded client IP is used — but only when the
/// request carries a valid service token, so external callers cannot spoof the header.
/// </summary>
public sealed class IdentityAuthRateLimitTests : IClassFixture<IdentityServiceFactory>
{
    private const int DefaultLimit = 20;
    private readonly HttpClient _client;

    public IdentityAuthRateLimitTests(IdentityServiceFactory factory) => _client = factory.CreateClient();

    private Task<HttpResponseMessage> Login(string? forwardedIp, bool withServiceToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/login")
        {
            Content = JsonContent.Create(new { username = "nobody-ratelimit", password = "wrong-password-123" })
        };
        if (forwardedIp != null) request.Headers.Add("X-Clinic-Client-IP", forwardedIp);
        if (withServiceToken) request.Headers.Add("X-Identity-Service-Token", IdentityServiceFactory.ServiceToken);
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task GatewayForwardedClients_HaveIndependentQuotas_AndEachIsLimited()
    {
        // Many distinct clients behind the Gateway: none is throttled by the others' traffic.
        for (var i = 0; i < DefaultLimit + 5; i++)
        {
            var res = await Login($"198.51.100.{i + 1}", withServiceToken: true);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, res.StatusCode);
        }

        // A single client is still limited.
        HttpStatusCode last = HttpStatusCode.OK;
        for (var i = 0; i <= DefaultLimit; i++)
            last = (await Login("203.0.113.77", withServiceToken: true)).StatusCode;
        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    [Fact]
    public async Task ForwardedIpWithoutServiceToken_IsIgnored_SoSpoofingDoesNotBypassTheLimit()
    {
        HttpStatusCode last = HttpStatusCode.OK;
        for (var i = 0; i <= DefaultLimit + 2; i++)
            last = (await Login($"192.0.2.{i + 1}", withServiceToken: false)).StatusCode;
        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }
}
