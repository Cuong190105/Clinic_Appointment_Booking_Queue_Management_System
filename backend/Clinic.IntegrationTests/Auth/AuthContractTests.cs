using Clinic.IntegrationTests.Fixtures;

namespace Clinic.IntegrationTests.Auth;

public sealed class AuthContractTests : IClassFixture<ApiContractFactory>
{
    private readonly HttpClient _client;

    public AuthContractTests(ApiContractFactory factory)
    {
        _client = factory.CreateAnonymousClient();
    }

    [Fact]
    public async Task Jwks_endpoint_returns_public_key_set_contract()
    {
        using var response = await _client.GetAsync("/.well-known/jwks.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var envelope = await ApiResponseAssertions.ReadEnvelopeAsync(response);
        Assert.Equal(JsonValueKind.Array, ApiResponseAssertions.Result(envelope).GetProperty("keys").ValueKind);
    }

    [Fact]
    public async Task Revoke_without_refresh_token_is_idempotent()
    {
        using var response = await _client.PostAsJsonAsync("/auth/revoke", new { refreshToken = "" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
