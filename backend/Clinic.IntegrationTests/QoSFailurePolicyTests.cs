using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Clinic.API.Models;
using Microsoft.AspNetCore.Builder;
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
/// Phase 8: Quality of Service (QoS), SLA/SLO policies, and boundary behavior verification.
/// Verifies rate-limiting enforcement, IP/Subject partitioning, spoofing prevention,
/// critical endpoint exemptions, downstream timeout/circuit-break failure mapping (504/502),
/// correlation ID end-to-end propagation, and sensitive data / PHI suppression.
/// </summary>
public sealed class QoSFailurePolicyTests : IClassFixture<QoSFailurePolicyTests.QoSTestWebApplicationFactory>
{
    private readonly QoSTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public QoSFailurePolicyTests(QoSTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task RateLimiter_EnforcesLimit_Returns429_WithRetryAfter_AndApiResponseEnvelope()
    {
        // Use an isolated authenticated user token to have a clean, predictable partition
        var testSubject = $"user-quota-{Guid.NewGuid():N}";
        var token = _factory.CreateTestJwtToken(testSubject);

        // Permit limit is configured to 5 in this test environment
        for (int i = 0; i < 5; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/.well-known/openid-configuration");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var okRes = await _client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, okRes.StatusCode);
        }

        // 6th request from the same subject within the window must be rejected
        using var burstReq = new HttpRequestMessage(HttpMethod.Get, "/.well-known/openid-configuration");
        burstReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var burstRes = await _client.SendAsync(burstReq);

        Assert.Equal(HttpStatusCode.TooManyRequests, burstRes.StatusCode);
        Assert.NotNull(burstRes.Headers.RetryAfter);
        Assert.Equal("60", burstRes.Headers.RetryAfter.Delta?.TotalSeconds.ToString("0"));

        var responseBody = await burstRes.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(responseBody);
        Assert.False(responseBody.IsSuccess);
        Assert.Contains(responseBody.ErrorMessages, err => err.Contains("Rate limit exceeded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RateLimiter_CannotBeBypassed_BySpoofingXForwardedForHeader()
    {
        // An anonymous attacker attempts to bypass rate limiting by changing X-Forwarded-For on each request.
        // Because the Gateway does not trust arbitrary untrusted proxies, it partitions by the real RemoteIpAddress.
        var realConnectionIp = $"198.51.100.{Random.Shared.Next(1, 250)}";

        for (int i = 0; i < 5; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/.well-known/openid-configuration");
            req.Headers.Add("X-Test-Connection-IP", realConnectionIp);
            req.Headers.Add("X-Forwarded-For", $"203.0.113.{i + 1}"); // Attacker tries to spoof
            using var okRes = await _client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, okRes.StatusCode);
        }

        // 6th request from the same connection IP with yet another spoofed X-Forwarded-For MUST be rejected (429)
        using var spoofedReq = new HttpRequestMessage(HttpMethod.Get, "/.well-known/openid-configuration");
        spoofedReq.Headers.Add("X-Test-Connection-IP", realConnectionIp);
        spoofedReq.Headers.Add("X-Forwarded-For", "203.0.113.99");
        using var throttledRes = await _client.SendAsync(spoofedReq);

        Assert.Equal(HttpStatusCode.TooManyRequests, throttledRes.StatusCode);
    }

    [Fact]
    public async Task RateLimiter_Partitions_Requests_Independently_By_JwtSubjectClaim()
    {
        var subjectAlpha = $"user-alpha-{Guid.NewGuid():N}";
        var subjectBeta = $"user-beta-{Guid.NewGuid():N}";

        var tokenAlpha = _factory.CreateTestJwtToken(subjectAlpha);
        var tokenBeta = _factory.CreateTestJwtToken(subjectBeta);

        // Exhaust permit limit for User Alpha
        for (int i = 0; i < 5; i++)
        {
            using var reqAlpha = new HttpRequestMessage(HttpMethod.Get, "/.well-known/openid-configuration");
            reqAlpha.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenAlpha);
            using var resAlpha = await _client.SendAsync(reqAlpha);
            Assert.Equal(HttpStatusCode.OK, resAlpha.StatusCode);
        }

        // User Alpha's 6th request gets throttled (429)
        using var throttledReqAlpha = new HttpRequestMessage(HttpMethod.Get, "/.well-known/openid-configuration");
        throttledReqAlpha.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenAlpha);
        using var throttledResAlpha = await _client.SendAsync(throttledReqAlpha);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttledResAlpha.StatusCode);

        // User Beta (different subject partition from the same client host) must NOT be throttled and succeed immediately
        using var reqBeta = new HttpRequestMessage(HttpMethod.Get, "/.well-known/openid-configuration");
        reqBeta.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenBeta);
        using var resBeta = await _client.SendAsync(reqBeta);
        Assert.Equal(HttpStatusCode.OK, resBeta.StatusCode);
    }

    [Fact]
    public async Task RateLimiter_Exempts_AllOperationalAndContractDiscoveryEndpoints()
    {
        var exhaustedSubject = $"user-exhausted-{Guid.NewGuid():N}";
        var token = _factory.CreateTestJwtToken(exhaustedSubject);

        // Exhaust the quota for this subject
        for (int i = 0; i < 5; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/.well-known/openid-configuration");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var res = await _client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        // Normal route is now throttled
        using var throttledReq = new HttpRequestMessage(HttpMethod.Get, "/.well-known/openid-configuration");
        throttledReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var throttledRes = await _client.SendAsync(throttledReq);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttledRes.StatusCode);

        // All required exempt endpoints MUST NOT be throttled (Status must not be 429)
        string[] exemptEndpoints = ["/healthz", "/readyz", "/metrics", "/openapi/v1.json", "/swagger/index.html"];

        foreach (var endpoint in exemptEndpoints)
        {
            for (int k = 0; k < 3; k++)
            {
                using var exemptReq = new HttpRequestMessage(HttpMethod.Get, endpoint);
                exemptReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var exemptRes = await _client.SendAsync(exemptReq);
                Assert.NotEqual(HttpStatusCode.TooManyRequests, exemptRes.StatusCode);
            }
        }
    }

    [Fact]
    public async Task DownstreamTimeout_Returns_504_GatewayTimeout_In_ApiResponse_Envelope()
    {
        // When downstream service takes longer than SLA budget or times out, Gateway must respond with 504 GatewayTimeout
        var isolatedToken = _factory.CreateTestJwtToken($"user-timeout-{Guid.NewGuid():N}");
        _factory.SimulateSchedulingTimeout = true;
        _factory.DownstreamTimeoutDelayMs = 120;
        try
        {
            var stopwatch = Stopwatch.StartNew();
            using var req = new HttpRequestMessage(HttpMethod.Get, "/schedules");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", isolatedToken);
            using var res = await _client.SendAsync(req);
            stopwatch.Stop();

            Assert.Equal(HttpStatusCode.GatewayTimeout, res.StatusCode);
            // Assert that the elapsed time meets or exceeds the downstream timeout deadline delay
            Assert.True(stopwatch.ElapsedMilliseconds >= 100, $"Expected elapsed >= 100ms, actual: {stopwatch.ElapsedMilliseconds}ms");

            var responseBody = await res.Content.ReadFromJsonAsync<ApiResponse<object>>();
            Assert.NotNull(responseBody);
            Assert.False(responseBody.IsSuccess);
            Assert.Contains(responseBody.ErrorMessages, err => err.Contains("timed out", StringComparison.OrdinalIgnoreCase) || err.Contains("SLA threshold", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _factory.SimulateSchedulingTimeout = false;
        }
    }

    [Fact]
    public async Task DownstreamUnavailable_Returns_502_BadGateway_In_ApiResponse_Envelope()
    {
        // When downstream service connection fails (network drop), Gateway must respond with 502 BadGateway in ApiResponse format
        var isolatedToken = _factory.CreateTestJwtToken($"user-down-{Guid.NewGuid():N}");
        _factory.SimulateSchedulingDown = true;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/schedules");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", isolatedToken);
            using var res = await _client.SendAsync(req);

            Assert.Equal(HttpStatusCode.BadGateway, res.StatusCode);
            var responseBody = await res.Content.ReadFromJsonAsync<ApiResponse<object>>();
            Assert.NotNull(responseBody);
            Assert.False(responseBody.IsSuccess);
            Assert.Contains(responseBody.ErrorMessages, err => err.Contains("unavailable", StringComparison.OrdinalIgnoreCase) || err.Contains("connection", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _factory.SimulateSchedulingDown = false;
        }
    }

    [Fact]
    public async Task CorrelationId_Propagates_AcrossMultipleRoutes_AndPreservesClientSuppliedValue()
    {
        string[] testRoutes = ["/schedules", "/.well-known/openid-configuration"];

        foreach (var route in testRoutes)
        {
            var expectedCorrelationId = Guid.NewGuid().ToString("D");

            using var request = new HttpRequestMessage(HttpMethod.Get, route);
            request.Headers.Add("X-Correlation-ID", expectedCorrelationId);

            using var response = await _client.SendAsync(request);

            // 1. Gateway must return the same correlation ID header to the client
            Assert.True(response.Headers.Contains("X-Correlation-ID"));
            var returnedHeader = response.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
            Assert.Equal(expectedCorrelationId, returnedHeader);
        }

        // Gateway must also propagate the correlation ID downstream to the microservice
        var downstreamExpectedId = Guid.NewGuid().ToString("D");
        using var schedReq = new HttpRequestMessage(HttpMethod.Get, "/schedules");
        schedReq.Headers.Add("X-Correlation-ID", downstreamExpectedId);
        using var schedRes = await _client.SendAsync(schedReq);
        Assert.Equal(downstreamExpectedId, _factory.LastReceivedDownstreamCorrelationId);
    }

    [Fact]
    public async Task SensitiveDataSanitization_LogsAndTelemetryNeverContainPasswordsOrPHI()
    {
        const string sensitivePassword = "SuperSecretAdminPassword987!";
        const string sensitivePhiSentinel = "DIAGNOSIS_PATIENT_HIV_STAGE_4_CONFIDENTIAL";

        // 1. Submit valid authentication payload containing password
        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new
            {
                username = "test_patient",
                password = sensitivePassword
            })
        };
        using var loginResponse = await _client.SendAsync(loginRequest);
        var loginBody = await loginResponse.Content.ReadAsStringAsync();

        // Ensure password is not echoed back in response body
        Assert.DoesNotContain(sensitivePassword, loginBody);

        // 2. Submit legitimate clinical request containing PHI to medical reports endpoint
        var doctorToken = _factory.CreateTestJwtToken(
            $"doctor-{Guid.NewGuid():N}",
            role: "Doctor",
            permissions: ["medical-report.create"]);

        using var medicalReportRequest = new HttpRequestMessage(HttpMethod.Post, "/medical-reports")
        {
            Content = JsonContent.Create(new SaveMedicalReportRequest
            {
                AppointmentId = Guid.NewGuid(),
                Symptoms = "Fever and persistent fatigue",
                Diagnosis = sensitivePhiSentinel,
                Notes = "Confidential clinical observations",
                Prescription = "Prescribed standard antiretroviral regimen",
                IsFinalize = false
            })
        };
        medicalReportRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", doctorToken);

        using var medicalReportResponse = await _client.SendAsync(medicalReportRequest);
        var medicalReportBody = await medicalReportResponse.Content.ReadAsStringAsync();

        // 3. Response body from Gateway must not expose unhandled error dumps or leaked secrets
        Assert.DoesNotContain(sensitivePassword, medicalReportBody);

        // 4. Application logs must never contain sensitive credentials or PHI
        var logs = _factory.CapturedLogOutput;
        Assert.DoesNotContain(sensitivePassword, logs);
        Assert.DoesNotContain(sensitivePhiSentinel, logs);

        // 5. OpenTelemetry traces / spans / tags must never record raw passwords or PHI sentinels
        var capturedActivities = _factory.CapturedActivities;
        foreach (var activity in capturedActivities)
        {
            foreach (var tag in activity.Tags)
            {
                Assert.DoesNotContain(sensitivePassword, tag.Value ?? string.Empty);
                Assert.DoesNotContain(sensitivePhiSentinel, tag.Value ?? string.Empty);
            }
            foreach (var evt in activity.Events)
            {
                Assert.DoesNotContain(sensitivePassword, evt.Name);
                Assert.DoesNotContain(sensitivePhiSentinel, evt.Name);
                foreach (var tag in evt.Tags)
                {
                    Assert.DoesNotContain(sensitivePassword, tag.Value?.ToString() ?? string.Empty);
                    Assert.DoesNotContain(sensitivePhiSentinel, tag.Value?.ToString() ?? string.Empty);
                }
            }
        }
    }

    public sealed class QoSTestWebApplicationFactory : WebApplicationFactory<Program>
    {
        public bool SimulateSchedulingTimeout { get; set; }
        public bool SimulateSchedulingDown { get; set; }
        public int DownstreamTimeoutDelayMs { get; set; } = 120;
        public string? LastReceivedDownstreamCorrelationId { get; set; }
        private readonly System.Text.StringBuilder _logBuffer = new();
        private readonly SymmetricSecurityKey _signingKey = new(System.Text.Encoding.UTF8.GetBytes("SuperSecretTestingKeyForQoSTestsOnly1234567890!"));
        private readonly List<Activity> _capturedActivities = new();
        private readonly ActivityListener _activityListener;

        public QoSTestWebApplicationFactory()
        {
            _activityListener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    lock (_capturedActivities)
                    {
                        _capturedActivities.Add(activity);
                    }
                }
            };
            ActivitySource.AddActivityListener(_activityListener);
        }

        public string CapturedLogOutput => _logBuffer.ToString();

        public IReadOnlyList<Activity> CapturedActivities
        {
            get
            {
                lock (_capturedActivities)
                {
                    return _capturedActivities.ToList();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _activityListener.Dispose();
            }
        }

        public string CreateTestJwtToken(string subject, string role = "Patient", string[]? permissions = null)
        {
            var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(ClaimTypes.NameIdentifier, subject),
                new Claim(ClaimTypes.Role, role)
            };
            if (permissions != null)
            {
                foreach (var perm in permissions)
                {
                    claims.Add(new Claim("permission", perm));
                }
            }
            var token = new JwtSecurityToken(
                issuer: "Clinic.Identity",
                audience: "Clinic.Services",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(new TestLogProvider(_logBuffer));
            });

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SkipDatabaseInitialization"] = "true",
                    ["SkipBackgroundWorkers"] = "true",
                    ["QoS:RateLimitPermitLimit"] = "5", // 5 requests per window for quick boundary verification
                    ["Identity:Authority"] = "http://identity.test",
                    ["Scheduling:Authority"] = "http://scheduling.test",
                    ["MedicalRecords:Authority"] = "http://medicalrecords.test",
                    ["Identity:RequireHttpsMetadata"] = "false",
                    ["Jwt:Key"] = "SuperSecretTestingKeyForQoSTestsOnly1234567890!",
                    ["Jwt:PublicBaseUrl"] = "http://localhost:5000",
                    ["Jwt:Issuer"] = "Clinic.Identity",
                    ["Jwt:Audience"] = "Clinic.Services"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddTransient<IStartupFilter, TestRemoteIpStartupFilter>();
                services.PostConfigure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>("IdentityJwt", options =>
                {
                    options.Configuration = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration();
                    options.TokenValidationParameters.IssuerSigningKey = _signingKey;
                });

                services.ConfigureHttpClientDefaults(clientBuilder =>
                {
                    clientBuilder.ConfigurePrimaryHttpMessageHandler(() => new QoSMockHttpHandler(this));
                });
            });
        }
    }

    public sealed class TestRemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    if (context.Request.Headers.TryGetValue("X-Test-Connection-IP", out var testIp) &&
                        IPAddress.TryParse(testIp.FirstOrDefault(), out var parsedIp))
                    {
                        context.Connection.RemoteIpAddress = parsedIp;
                    }
                    await nextMiddleware();
                });
                next(app);
            };
        }
    }

    private sealed class QoSMockHttpHandler : HttpMessageHandler
    {
        private readonly QoSTestWebApplicationFactory _factory;

        public QoSMockHttpHandler(QoSTestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.TryGetValues("X-Correlation-ID", out var cid))
            {
                _factory.LastReceivedDownstreamCorrelationId = cid.FirstOrDefault();
            }

            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (_factory.SimulateSchedulingTimeout && path.Contains("schedules", StringComparison.OrdinalIgnoreCase))
            {
                // Simulate an authentic downstream network cancellation / deadline timeout
                var delayMs = _factory.DownstreamTimeoutDelayMs;
                await Task.Delay(delayMs, cancellationToken);
                throw new TaskCanceledException($"Downstream microservice call timed out after {delayMs}ms.");
            }

            if (_factory.SimulateSchedulingDown && path.Contains("schedules", StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpRequestException("Connection refused by downstream microservice.");
            }

            if (path.Contains("login", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"isSuccess\":true,\"data\":{\"token\":\"mock-jwt-token\",\"refreshToken\":\"mock-refresh\"}}", System.Text.Encoding.UTF8, "application/json")
                };
            }

            if (path.Contains("medical-records", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"isSuccess\":true,\"data\":{\"id\":\"" + Guid.NewGuid() + "\"}}", System.Text.Encoding.UTF8, "application/json")
                };
            }

            if (path.Contains("schedules", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"data\":[]}", System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class TestLogProvider : ILoggerProvider
    {
        private readonly System.Text.StringBuilder _buffer;
        public TestLogProvider(System.Text.StringBuilder buffer) => _buffer = buffer;
        public ILogger CreateLogger(string categoryName) => new TestLogger(_buffer);
        public void Dispose() { }
    }

    private sealed class TestLogger : ILogger
    {
        private readonly System.Text.StringBuilder _buffer;
        public TestLogger(System.Text.StringBuilder buffer) => _buffer = buffer;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_buffer)
            {
                _buffer.AppendLine(formatter(state, exception));
                if (exception != null)
                {
                    _buffer.AppendLine(exception.ToString());
                }
            }
        }
    }
}

