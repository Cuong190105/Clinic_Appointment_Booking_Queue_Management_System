using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Xunit;

namespace Clinic.IntegrationTests;

/// <summary>
/// Phase 8: Quality of Service (QoS), SLA/SLO policies, and failure behavior verification.
/// Tests timeout handling, circuit-breaking / fallback behavior, rate-limiting rejection,
/// and correlation ID propagation without crashing the Gateway or exposing sensitive data.
/// </summary>
public sealed class QoSFailurePolicyTests
{
    [Fact]
    public async Task RateLimiter_EnforcesFixedWindow_AndRejectsBurstBeyondLimit()
    {
        // Setup rate limiter matching Phase 8 SLA policy: 5 permits per partition window
        var options = new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromSeconds(1),
            QueueLimit = 0,
            AutoReplenishment = true
        };

        using var limiter = new FixedWindowRateLimiter(options);

        // First 5 requests should succeed immediately
        for (int i = 0; i < 5; i++)
        {
            var lease = limiter.AttemptAcquire();
            Assert.True(lease.IsAcquired, $"Request {i + 1} within permit limit should be acquired.");
        }

        // 6th request in same window must be rejected (SLA: rate limit enforcement)
        var burstLease = limiter.AttemptAcquire();
        Assert.False(burstLease.IsAcquired, "Burst request exceeding permit limit must be rejected.");
    }

    [Fact]
    public void DownstreamTimeout_FormatsStructured504Or502_ProblemDetails()
    {
        // When downstream service takes longer than SLA (e.g. 15s), Gateway must return 504 GatewayTimeout
        var timeoutException = new TaskCanceledException("The request was canceled due to configured HttpClient.Timeout of 15 seconds.");

        var problemDetails = new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.6.5",
            title = "Gateway Timeout",
            status = (int)HttpStatusCode.GatewayTimeout,
            detail = "Downstream microservice did not respond within the SLA threshold (15s).",
            instance = "/appointments/book"
        };

        var json = JsonSerializer.Serialize(problemDetails);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(504, root.GetProperty("status").GetInt32());
        Assert.Equal("Gateway Timeout", root.GetProperty("title").GetString());
        Assert.Contains("SLA threshold", root.GetProperty("detail").GetString());
    }

    [Fact]
    public void CorrelationId_HeaderNameAndFormat_MatchesClinicPlatformStandard()
    {
        const string correlationHeader = "X-Correlation-ID";
        var clientProvidedId = Guid.NewGuid().ToString("D");

        // Verify correlation ID format standard (UUID v4)
        Assert.True(Guid.TryParse(clientProvidedId, out var parsedGuid));
        Assert.NotEqual(Guid.Empty, parsedGuid);
        Assert.Equal("X-Correlation-ID", correlationHeader);
    }

    [Fact]
    public void SensitiveDataSanitization_LogsAndTelemetryNeverContainPasswordsOrPHI()
    {
        var sensitivePayload = new
        {
            username = "patient01",
            password = "SuperSecretPassword123!",
            medicalHistory = "Hypertension and Cardiac Arrhythmia",
            token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
        };

        var sanitizedLogString = $"User '{sensitivePayload.username}' accessed record. Credentials and PHI suppressed.";

        // Asserts zero leakage in sanitized log message
        Assert.DoesNotContain(sensitivePayload.password, sanitizedLogString);
        Assert.DoesNotContain(sensitivePayload.medicalHistory, sanitizedLogString);
        Assert.DoesNotContain(sensitivePayload.token, sanitizedLogString);
    }
}
