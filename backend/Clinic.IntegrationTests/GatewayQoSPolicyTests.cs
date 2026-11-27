using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Clinic.IntegrationTests;

/// <summary>
/// SLI_SLO_MATRIX §5.2–5.3: tiered rate limits, sensitive-route limits, downstream deadlines,
/// retry-only-for-safe-methods and circuit-breaker fail-fast/recovery at the Gateway.
/// Each test uses its own factory so breaker/limiter state never leaks between tests.
/// </summary>
public sealed class GatewayQoSPolicyTests
{
    [Fact]
    public async Task StaffTier_HasHigherQuotaThanPatientTier()
    {
        await using var factory = new GatewayQoSFactory(new() { ["QoS:RateLimitPermitLimit"] = "5", ["QoS:StaffRateLimitPermitLimit"] = "20" });
        var client = factory.CreateClient();
        var patient = factory.Token("patient-tier", "Patient");
        var receptionist = factory.Token("staff-tier", "Receptionist");

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Get, "/schedules", patient)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Send(client, HttpMethod.Get, "/schedules", patient)).StatusCode);

        for (var i = 0; i < 15; i++)
            Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Get, "/schedules", receptionist)).StatusCode);
    }

    [Fact]
    public async Task AuthRoutes_AreLimitedPerIp_IndependentlyOfGeneralQuota()
    {
        await using var factory = new GatewayQoSFactory(new() { ["QoS:RateLimitPermitLimit"] = "50", ["QoS:AuthRateLimitPermitLimit"] = "3" });
        var client = factory.CreateClient();
        const string ip = "203.0.113.50";

        for (var i = 0; i < 3; i++)
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Login(client, ip)).StatusCode);

        var throttled = await Login(client, ip);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.NotNull(throttled.Headers.RetryAfter?.Delta);
        Assert.InRange(throttled.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 1, 60);

        // Other anonymous traffic from the same IP is still within the general quota.
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Get, "/schedules", null, ip)).StatusCode);
        // A different IP has its own login quota.
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Login(client, "203.0.113.51")).StatusCode);
    }

    [Fact]
    public async Task BookingCreate_IsLimitedPerPatient_WhileReadsContinue()
    {
        await using var factory = new GatewayQoSFactory(new() { ["QoS:RateLimitPermitLimit"] = "50", ["QoS:BookingRateLimitPermitLimit"] = "2" });
        var client = factory.CreateClient();
        var patient = factory.Token("booking-limit", "Patient", "appointment.create");

        for (var i = 0; i < 2; i++)
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Book(client, patient)).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Book(client, patient)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Get, "/schedules", patient)).StatusCode);

        var otherPatient = factory.Token("booking-limit-2", "Patient", "appointment.create");
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Book(client, otherPatient)).StatusCode);
    }

    [Fact]
    public async Task SafeMethods_AreRetried_OnTransientDownstreamFailure()
    {
        await using var factory = new GatewayQoSFactory();
        var client = factory.CreateClient();
        factory.Handler.SchedulingFailuresRemaining = 1;

        var response = await Send(client, HttpMethod.Get, "/schedules", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, factory.Handler.SchedulingCalls);
    }

    [Fact]
    public async Task UnsafeMethods_AreNeverRetried_ByTheGateway()
    {
        await using var factory = new GatewayQoSFactory();
        var client = factory.CreateClient();
        factory.Handler.AppointmentAlwaysFails = true;
        var patient = factory.Token("no-retry", "Patient", "appointment.create");

        var response = await Book(client, patient);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, factory.Handler.AppointmentPostCalls);
    }

    [Fact]
    public async Task SlowDownstream_IsCutOffByTotalDeadline_With504()
    {
        await using var factory = new GatewayQoSFactory(new()
        {
            ["QoS:Downstream:TotalTimeoutMs"] = "800",
            ["QoS:Downstream:AttemptTimeoutMs"] = "300"
        });
        var client = factory.CreateClient();
        factory.Handler.SchedulingDelay = TimeSpan.FromSeconds(10);

        var stopwatch = Stopwatch.StartNew();
        var response = await Send(client, HttpMethod.Get, "/schedules", null);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Deadline not enforced: {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task CircuitBreaker_FailsFastWith503_ThenRecovers()
    {
        await using var factory = new GatewayQoSFactory(new()
        {
            ["QoS:Downstream:BreakerMinimumThroughput"] = "4",
            ["QoS:Downstream:BreakDurationMs"] = "700",
            ["QoS:Downstream:MaxRetryAttempts"] = "1"
        });
        var client = factory.CreateClient();
        factory.Handler.SchedulingAlwaysFails = true;

        for (var i = 0; i < 4; i++)
            await Send(client, HttpMethod.Get, "/schedules", null);

        var callsBeforeOpen = factory.Handler.SchedulingCalls;
        var stopwatch = Stopwatch.StartNew();
        var open = await Send(client, HttpMethod.Get, "/schedules", null);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, open.StatusCode);
        Assert.NotNull(open.Headers.RetryAfter);
        Assert.Equal(callsBeforeOpen, factory.Handler.SchedulingCalls); // short-circuited, downstream not called
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(500), $"Open circuit should fail fast: {stopwatch.Elapsed}.");

        factory.Handler.SchedulingAlwaysFails = false;
        await Task.Delay(900);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Get, "/schedules", null)).StatusCode);
    }

    [Fact]
    public async Task DependencyFlaggedFailures_DoNotOpenTheBreaker_SoUnrelatedRoutesKeepWorking()
    {
        // Appointment returns 503 + X-Clinic-Dependency-Failure while Scheduling is down. The Appointment
        // service itself is healthy, so the Gateway must keep routing reads of existing appointments to it.
        await using var factory = new GatewayQoSFactory(new()
        {
            ["QoS:Downstream:BreakerMinimumThroughput"] = "4",
            ["QoS:BookingRateLimitPermitLimit"] = "100"
        });
        var client = factory.CreateClient();
        factory.Handler.AppointmentAlwaysFails = true;
        factory.Handler.AppointmentFailureIsDependency = true;
        var patient = factory.Token("dependency-failure", "Patient", "appointment.create", "appointment.view");

        for (var i = 0; i < 8; i++)
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Book(client, patient)).StatusCode);

        Assert.Equal(8, factory.Handler.AppointmentPostCalls); // never short-circuited
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Get, $"/appointments/{Guid.NewGuid()}", patient)).StatusCode);
    }

    [Fact]
    public async Task IdentityLoadShedding_IsPassedThrough_AndDoesNotOpenTheBreaker()
    {
        // During a login burst Identity answers 503 + X-Clinic-Load-Shed + Retry-After. The client must see
        // "busy, retry" (not "wrong password"), and the Identity breaker must stay closed so refresh/JWKS work.
        await using var factory = new GatewayQoSFactory(new()
        {
            ["QoS:Downstream:BreakerMinimumThroughput"] = "4",
            ["QoS:AuthRateLimitPermitLimit"] = "100"
        });
        var client = factory.CreateClient();
        factory.Handler.IdentityShedsLoad = true;

        for (var i = 0; i < 8; i++)
        {
            var response = await Login(client, "203.0.113.70");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.True(response.Headers.Contains(Clinic.API.Resilience.DownstreamResilience.LoadShedHeader));
            Assert.Equal(TimeSpan.FromSeconds(2), response.Headers.RetryAfter?.Delta);
            Assert.DoesNotContain("incorrect", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(8, factory.Handler.IdentityCalls); // never short-circuited by the breaker
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string? token, string? ip = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Test-Connection-IP", ip ?? "198.51.100.10");
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> Login(HttpClient client, string ip)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new { username = "patient1", password = "irrelevant-for-mock" })
        };
        request.Headers.Add("X-Test-Connection-IP", ip);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> Book(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/appointments")
        {
            Content = JsonContent.Create(new { workScheduleId = Guid.NewGuid(), timeSlot = "09:00:00", reason = "QoS test" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        request.Headers.Add("X-Test-Connection-IP", "198.51.100.20");
        return await client.SendAsync(request);
    }

    private sealed class GatewayQoSFactory : WebApplicationFactory<Program>
    {
        private const string SigningSecret = "SuperSecretTestingKeyForGatewayQoSTests1234567890!";
        private readonly SymmetricSecurityKey _signingKey = new(System.Text.Encoding.UTF8.GetBytes(SigningSecret));
        private readonly Dictionary<string, string?> _overrides;

        public GatewayQoSFactory(Dictionary<string, string?>? overrides = null) => _overrides = overrides ?? new();

        public ProgrammableDownstreamHandler Handler { get; } = new();

        public string Token(string subject, string role, params string[] permissions)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, subject),
                new(ClaimTypes.NameIdentifier, subject),
                new(ClaimTypes.Role, role)
            };
            claims.AddRange(permissions.Select(p => new Claim("permission", p)));
            var token = new JwtSecurityToken("Clinic.Identity", "Clinic.Services", claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["SkipDatabaseInitialization"] = "true",
                    ["SkipBackgroundWorkers"] = "true",
                    ["QoS:RateLimitPermitLimit"] = "1000",
                    ["Identity:Authority"] = "http://identity.test",
                    ["Scheduling:Authority"] = "http://scheduling.test",
                    ["Appointment:Authority"] = "http://appointment.test",
                    ["Identity:RequireHttpsMetadata"] = "false",
                    ["Jwt:Issuer"] = "Clinic.Identity",
                    ["Jwt:Audience"] = "Clinic.Services"
                };
                foreach (var (key, value) in _overrides) settings[key] = value;
                config.AddInMemoryCollection(settings);
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter, QoSFailurePolicyTests.TestRemoteIpStartupFilter>();
                services.PostConfigure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>("IdentityJwt", options =>
                {
                    options.Configuration = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration();
                    options.TokenValidationParameters.IssuerSigningKey = _signingKey;
                });
                services.ConfigureHttpClientDefaults(clientBuilder =>
                    clientBuilder.ConfigurePrimaryHttpMessageHandler(() => Handler.CreateInstance()));
            });
        }
    }

    internal sealed class ProgrammableDownstreamHandler
    {
        private int _schedulingCalls;
        private int _appointmentPostCalls;
        private int _schedulingFailuresRemaining;
        private int _identityCalls;

        public int SchedulingCalls => Volatile.Read(ref _schedulingCalls);
        public int AppointmentPostCalls => Volatile.Read(ref _appointmentPostCalls);
        public int IdentityCalls => Volatile.Read(ref _identityCalls);
        public volatile bool IdentityShedsLoad;
        public int SchedulingFailuresRemaining { set => Volatile.Write(ref _schedulingFailuresRemaining, value); }
        public volatile bool SchedulingAlwaysFails;
        public volatile bool AppointmentAlwaysFails;
        public volatile bool AppointmentFailureIsDependency;
        public TimeSpan SchedulingDelay { get; set; } = TimeSpan.Zero;

        public HttpMessageHandler CreateInstance() => new Instance(this);

        private sealed class Instance(ProgrammableDownstreamHandler owner) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var host = request.RequestUri?.Host ?? string.Empty;
                if (host.StartsWith("scheduling", StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Increment(ref owner._schedulingCalls);
                    if (owner.SchedulingDelay > TimeSpan.Zero)
                        await Task.Delay(owner.SchedulingDelay, cancellationToken);
                    if (owner.SchedulingAlwaysFails || Interlocked.Decrement(ref owner._schedulingFailuresRemaining) >= 0)
                        return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                    return Json("{\"isSuccess\":true,\"result\":[]}");
                }

                if (host.StartsWith("appointment", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
                {
                    Interlocked.Increment(ref owner._appointmentPostCalls);
                    if (!owner.AppointmentAlwaysFails)
                        return Json("{\"id\":\"" + Guid.NewGuid() + "\",\"status\":\"Pending\"}", HttpStatusCode.Created);
                    var failure = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") };
                    if (owner.AppointmentFailureIsDependency)
                        failure.Headers.Add(Clinic.API.Resilience.DownstreamResilience.DependencyFailureHeader, "scheduling");
                    return failure;
                }

                if (host.StartsWith("identity", StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Increment(ref owner._identityCalls);
                    if (owner.IdentityShedsLoad)
                    {
                        var shed = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") };
                        shed.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(2));
                        shed.Headers.Add(Clinic.API.Resilience.DownstreamResilience.LoadShedHeader, "password-hashing");
                        return shed;
                    }
                    return Json("{\"accessToken\":\"mock\",\"refreshToken\":\"mock\"}");
                }

                return Json("{}");
            }

            private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
                new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
