using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Clinic.Application.Contracts;
using Clinic.IntegrationTests.Fixtures;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Clinic.IntegrationTests.Auth;

public sealed class GatewayAuthIntegrationTests : IClassFixture<GatewayAuthTestFactory>
{
    private readonly GatewayAuthTestFactory _factory;
    private readonly HttpClient _client;

    public GatewayAuthIntegrationTests(GatewayAuthTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Discovery_Endpoint_Returns_Valid_Configuration()
    {
        using var response = await _client.GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        Assert.Equal("Clinic.Identity", root.GetProperty("issuer").GetString());
        Assert.Equal("http://localhost:5000/.well-known/jwks.json", root.GetProperty("jwks_uri").GetString());
        Assert.Contains("RS256", root.GetProperty("id_token_signing_alg_values_supported").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task Jwks_Endpoint_Proxies_Keys_From_Identity()
    {
        using var response = await _client.GetAsync("/.well-known/jwks.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var keys = document.RootElement.GetProperty("keys");
        Assert.Equal(JsonValueKind.Array, keys.ValueKind);
        Assert.True(keys.GetArrayLength() >= 1);

        var firstKey = keys[0];
        Assert.Equal("RSA", firstKey.GetProperty("kty").GetString());
        Assert.Equal("RS256", firstKey.GetProperty("alg").GetString());
        Assert.Equal(_factory.KeyRing.ActiveKey.Kid, firstKey.GetProperty("kid").GetString());
    }

    [Fact]
    public async Task Valid_RS256_Token_Authenticates_Successfully()
    {
        var token = _factory.KeyRing.GenerateToken(
            sub: Guid.NewGuid().ToString(),
            role: "Patient",
            permissions: ["patient.view.own", "queue.call-next"]);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/permissions");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        var array = root.TryGetProperty("result", out var res) ? res : root;
        var permissions = array.EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("patient.view.own", permissions);
        Assert.Contains("queue.call-next", permissions);
    }

    [Fact]
    public async Task Expired_Token_Is_Rejected_With_401()
    {
        var expiredToken = _factory.KeyRing.GenerateToken(
            sub: Guid.NewGuid().ToString(),
            role: "Patient",
            expires: DateTime.UtcNow.AddMinutes(-5));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/permissions");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expiredToken);

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_Signature_Token_Is_Rejected_With_401()
    {
        using var unknownRsa = RSA.Create(2048);
        var unknownKey = ("unknown-kid", unknownRsa);
        var foreignToken = _factory.KeyRing.GenerateToken(
            sub: Guid.NewGuid().ToString(),
            role: "Patient",
            signingKey: unknownKey);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/permissions");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", foreignToken);

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Permission_Policy_Denies_Request_When_Permission_Missing()
    {
        var token = _factory.KeyRing.GenerateToken(
            sub: Guid.NewGuid().ToString(),
            role: "Patient",
            permissions: ["patient.view.own"]);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/specialties")
        {
            Content = JsonContent.Create(new { name = "Neurology", description = "Brain and nerves" })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Permission_Policy_Allows_Request_When_Permission_Present()
    {
        var token = _factory.KeyRing.GenerateToken(
            sub: Guid.NewGuid().ToString(),
            role: "Staff",
            permissions: ["specialty.manage"]);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/specialties")
        {
            Content = JsonContent.Create(new { name = "Cardiology", description = "Heart care" })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Key_Rotation_Validates_Both_Old_And_New_Keys()
    {
        var oldKey = _factory.KeyRing.ActiveKey;
        var tokenSignedWithOldKey = _factory.KeyRing.GenerateToken(
            sub: Guid.NewGuid().ToString(),
            role: "Patient",
            permissions: ["patient.view.own"],
            signingKey: oldKey);

        var newKey = _factory.KeyRing.Rotate();
        var tokenSignedWithNewKey = _factory.KeyRing.GenerateToken(
            sub: Guid.NewGuid().ToString(),
            role: "Patient",
            permissions: ["patient.view.own"],
            signingKey: newKey);

        using (var reqOld = new HttpRequestMessage(HttpMethod.Get, "/auth/permissions"))
        {
            reqOld.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenSignedWithOldKey);
            using var resOld = await _client.SendAsync(reqOld);
            Assert.Equal(HttpStatusCode.OK, resOld.StatusCode);
        }

        using (var reqNew = new HttpRequestMessage(HttpMethod.Get, "/auth/permissions"))
        {
            reqNew.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenSignedWithNewKey);
            using var resNew = await _client.SendAsync(reqNew);
            Assert.Equal(HttpStatusCode.OK, resNew.StatusCode);
        }
    }

    [Fact]
    public async Task Jwks_Temporarily_Unavailable_Resiliency()
    {
        var token = _factory.KeyRing.GenerateToken(
            sub: Guid.NewGuid().ToString(),
            role: "Patient",
            permissions: ["patient.view.own"]);

        _factory.SimulateUpstreamJwksFailure = true;

        using var jwksResponse = await _client.GetAsync("/.well-known/jwks.json");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, jwksResponse.StatusCode);

        using var authRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/permissions");
        authRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var authResponse = await _client.SendAsync(authRequest);
        Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);

        _factory.SimulateUpstreamJwksFailure = false;
    }

    [Fact]
    public async Task Correlation_Id_Header_Is_Propagated_And_Returned()
    {
        var customCorrelationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new { username = "sample", password = "Password123!" })
        };
        request.Headers.Add("X-Correlation-ID", customCorrelationId);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var correlationHeaderValues));
        Assert.Equal(customCorrelationId, correlationHeaderValues.FirstOrDefault());
        Assert.Equal(customCorrelationId, _factory.LastReceivedUpstreamCorrelationId);
    }

    [Fact]
    public async Task Correlation_Id_Is_Generated_When_Not_Provided()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new { username = "sample", password = "Password123!" })
        };

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var correlationHeaderValues));
        var generatedId = correlationHeaderValues.FirstOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(generatedId));
        Assert.Equal(generatedId, _factory.LastReceivedUpstreamCorrelationId);
    }
}

public sealed class GatewayFeatureFlagTests : IClassFixture<GatewayAuthDisabledFeatureFlagFactory>
{
    private readonly HttpClient _client;

    public GatewayFeatureFlagTests(GatewayAuthDisabledFeatureFlagFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Feature_Flag_Disables_Identity_Route()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new { username = "sample", password = "Password123!" })
        };

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("disabled by configuration", content, StringComparison.OrdinalIgnoreCase);
    }
}

public class GatewayAuthTestFactory : WebApplicationFactory<Program>
{
    protected readonly bool UseIdentityService;
    public TestKeyRing KeyRing { get; } = new();
    public bool SimulateUpstreamJwksFailure { get; set; }
    public string? LastReceivedUpstreamCorrelationId { get; set; }

    public GatewayAuthTestFactory() : this(true) { }

    protected GatewayAuthTestFactory(bool useIdentityService)
    {
        UseIdentityService = useIdentityService;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SkipDatabaseInitialization"] = "true",
                ["SkipBackgroundWorkers"] = "true",
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost,1433;Database=ClinicBaselineTests_Gateway;User Id=sa;Password=ClinicDev!2026StrongPw;TrustServerCertificate=True;",
                ["Identity:Authority"] = "http://identity.test",
                ["Identity:RequireHttpsMetadata"] = "false",
                ["Jwt:PublicBaseUrl"] = "http://localhost:5000",
                ["Jwt:Issuer"] = "Clinic.Identity",
                ["Jwt:Audience"] = "Clinic.Services",
                ["Features:UseIdentityService"] = UseIdentityService ? "true" : "false"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory>(new GatewayMockHttpClientFactory(this));

            services.RemoveAll<ISender>();
            services.AddSingleton<ISender>(new DummyMediatRSender());

            services.PostConfigure<JwtBearerOptions>("IdentityJwt", options =>
            {
                options.Configuration = new OpenIdConnectConfiguration();
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                options.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, kid, _) =>
                    KeyRing.GetValidationKeys(kid);
            });
        });
    }

    private sealed class DummyMediatRSender : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (typeof(TResponse) == typeof(SpecialtyDto))
            {
                var dto = new SpecialtyDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Cardiology",
                    Description = "Heart care"
                };
                return Task.FromResult((TResponse)(object)dto);
            }
            return Task.FromResult(default(TResponse)!);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => Task.CompletedTask;

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => Task.FromResult<object?>(null);

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class GatewayMockHttpClientFactory : IHttpClientFactory
    {
        private readonly GatewayAuthTestFactory _parent;

        public GatewayMockHttpClientFactory(GatewayAuthTestFactory parent)
        {
            _parent = parent;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new GatewayMockHttpHandler(_parent))
            {
                BaseAddress = new Uri("http://identity.test/")
            };
        }
    }

    private sealed class GatewayMockHttpHandler : HttpMessageHandler
    {
        private readonly GatewayAuthTestFactory _parent;

        public GatewayMockHttpHandler(GatewayAuthTestFactory parent)
        {
            _parent = parent;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.TryGetValues("X-Correlation-ID", out var cidValues))
            {
                _parent.LastReceivedUpstreamCorrelationId = cidValues.FirstOrDefault();
            }

            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (path.EndsWith(".well-known/jwks.json", StringComparison.OrdinalIgnoreCase))
            {
                if (_parent.SimulateUpstreamJwksFailure)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                }

                var jwks = _parent.KeyRing.GetJwksJson();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jwks, System.Text.Encoding.UTF8, "application/json")
                });
            }

            if (path.EndsWith("/v1/auth/login", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                var responseContent = JsonSerializer.Serialize(new
                {
                    accessToken = _parent.KeyRing.GenerateToken(Guid.NewGuid().ToString()),
                    refreshToken = "mock-refresh-token-12345",
                    roles = new[] { "Patient" }
                });

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}

public sealed class GatewayAuthDisabledFeatureFlagFactory : GatewayAuthTestFactory
{
    public GatewayAuthDisabledFeatureFlagFactory() : base(useIdentityService: false) { }
}

public sealed class TestKeyRing
{
    private readonly List<(string Kid, RSA Rsa)> _keys = [];
    public (string Kid, RSA Rsa) ActiveKey { get; private set; }

    public TestKeyRing()
    {
        ActiveKey = CreateKey();
    }

    public (string Kid, RSA Rsa) CreateKey()
    {
        var rsa = RSA.Create(2048);
        var p = rsa.ExportParameters(false);
        var kid = Base64UrlEncoder.Encode(SHA256.HashData(p.Modulus!)[..12]);
        var key = (kid, rsa);
        _keys.Add(key);
        ActiveKey = key;
        return key;
    }

    public (string Kid, RSA Rsa) Rotate()
    {
        return CreateKey();
    }

    public string GetJwksJson()
    {
        var keys = _keys.Select(k =>
        {
            var p = k.Rsa.ExportParameters(false);
            return new
            {
                kty = "RSA",
                use = "sig",
                alg = "RS256",
                kid = k.Kid,
                n = Base64UrlEncoder.Encode(p.Modulus!),
                e = Base64UrlEncoder.Encode(p.Exponent!)
            };
        });
        return JsonSerializer.Serialize(new { keys });
    }

    public IEnumerable<SecurityKey> GetValidationKeys(string? kid)
    {
        return _keys
            .Where(k => kid == null || k.Kid == kid)
            .Select(k => (SecurityKey)new RsaSecurityKey(k.Rsa) { KeyId = k.Kid });
    }

    public string GenerateToken(
        string sub,
        string role = "Patient",
        IEnumerable<string>? permissions = null,
        DateTime? expires = null,
        (string Kid, RSA Rsa)? signingKey = null,
        string issuer = "Clinic.Identity",
        string audience = "Clinic.Services")
    {
        var key = signingKey ?? ActiveKey;
        var credentials = new SigningCredentials(
            new RsaSecurityKey(key.Rsa) { KeyId = key.Kid },
            SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, sub),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role),
            new("personId", Guid.NewGuid().ToString())
        };

        if (permissions != null)
        {
            foreach (var perm in permissions)
            {
                claims.Add(new Claim("permission", perm));
            }
        }

        var now = DateTime.UtcNow;
        var exp = expires ?? now.AddMinutes(15);
        var nbf = exp < now ? exp.AddMinutes(-5) : now.AddMinutes(-1);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: nbf,
            expires: exp,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
