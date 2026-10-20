using Clinic.IntegrationTests.Fixtures;

namespace Clinic.IntegrationTests.Auth;

public sealed class AuthContractTests : IClassFixture<ApiContractFactory>
{
    private readonly ApiContractFactory _factory;
    private readonly HttpClient _client;

    public AuthContractTests(ApiContractFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAnonymousClient();
    }

    [Fact]
    public async Task Jwks_endpoint_returns_raw_public_key_set_contract()
    {
        using var response = await _client.GetAsync("/.well-known/jwks.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("keys").ValueKind);
    }

    [Fact]
    public async Task Discovery_uses_configured_public_issuer_and_jwks_url()
    {
        using var response = await _client.GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("http://localhost:5000", document.RootElement.GetProperty("issuer").GetString());
        Assert.Equal("http://localhost:5000/.well-known/jwks.json", document.RootElement.GetProperty("jwks_uri").GetString());
    }

    [Fact]
    public async Task Revoke_without_refresh_token_is_idempotent()
    {
        using var response = await _client.PostAsJsonAsync("/auth/revoke", new { refreshToken = "" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Login_returns_tokens_from_identity_service()
    {
        using var response = await _client.PostAsJsonAsync("/auth/login", new { username = "sample", password = "Password123!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("refreshToken=identity-refresh-token", StringComparison.Ordinal));
        using var envelope = await ApiResponseAssertions.ReadEnvelopeAsync(response);
        var result = ApiResponseAssertions.Result(envelope);
        Assert.Equal("identity-access-token", result.GetProperty("accessToken").GetString());
        Assert.Equal("Patient", result.GetProperty("roles")[0].GetString());
    }

    [Fact]
    public async Task Me_returns_identity_profile()
    {
        using var client = _factory.CreateAuthenticatedClient();
        using var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var envelope = await ApiResponseAssertions.ReadEnvelopeAsync(response);
        Assert.Equal("Identity Admin User", ApiResponseAssertions.Result(envelope).GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task Refresh_does_not_fall_back_to_clinic_when_identity_rejects_token()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        request.Headers.TryAddWithoutValidation("Cookie", "refreshToken=invalid-identity-token");
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("refreshToken=;", StringComparison.Ordinal));
    }
}
