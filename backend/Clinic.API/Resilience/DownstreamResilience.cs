using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Clinic.API.Resilience;

/// <summary>
/// Gateway -> downstream QoS policy (SLI_SLO_MATRIX §5.2):
/// total deadline 5s per call, per-attempt timeout 3s (4s for booking/Appointment),
/// up to 2 jittered retries for idempotent (safe) HTTP methods only,
/// circuit breaker opening at >= 50% failures over 30s (min. 20 calls) for 15s.
/// </summary>
public static class DownstreamResilience
{
    public static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan DefaultAttemptTimeout = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan BookingAttemptTimeout = TimeSpan.FromSeconds(4);
    // Identity verifies passwords with BCrypt (CPU-bound by design); bursts of logins queue on its
    // 0.5 CPU, so this hop gets a longer budget than the 5s default (SLI_SLO_MATRIX §5.2).
    public static readonly TimeSpan IdentityTotalTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan IdentityAttemptTimeout = TimeSpan.FromSeconds(9);
    public const int MaxRetryAttempts = 2;
    public const double BreakerFailureRatio = 0.5;
    public const int BreakerMinimumThroughput = 20;
    public static readonly TimeSpan BreakerSamplingDuration = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan BreakerBreakDuration = TimeSpan.FromSeconds(15);

    /// <summary>Set by a service when a 5xx was caused by one of ITS dependencies (e.g. Appointment → Scheduling).</summary>
    public const string DependencyFailureHeader = "X-Clinic-Dependency-Failure";

    /// <summary>
    /// Set by a service that deliberately sheds load (e.g. Identity when all password-hashing slots are
    /// busy). The service is healthy and protecting itself; the client should honour Retry-After.
    /// </summary>
    public const string LoadShedHeader = "X-Clinic-Load-Shed";

    /// <summary>
    /// Transient failure of the called service itself. A 5xx flagged as a dependency failure or as load
    /// shedding means the called service is healthy and degrading correctly, so it must not trip this
    /// hop's breaker (otherwise e.g. a Scheduling outage would also block reading existing appointments,
    /// and a login burst would also block token refresh).
    /// </summary>
    public static bool IsOwnTransientFailure(Outcome<HttpResponseMessage> outcome) =>
        HttpClientResiliencePredicates.IsTransient(outcome) &&
        !(outcome.Result?.Headers.Contains(DependencyFailureHeader) ?? false) &&
        !(outcome.Result?.Headers.Contains(LoadShedHeader) ?? false);

    public static IHttpClientBuilder AddClinicDownstreamResilience(
        this IHttpClientBuilder builder,
        IConfiguration configuration,
        TimeSpan? attemptTimeout = null,
        TimeSpan? totalTimeout = null)
    {
        // HttpClient.Timeout must not pre-empt the resilience pipeline deadlines.
        builder.ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan);

        builder.AddStandardResilienceHandler(options =>
        {
            var total = Read(configuration, "QoS:Downstream:TotalTimeoutMs", totalTimeout ?? TotalTimeout);
            var attempt = Read(configuration, "QoS:Downstream:AttemptTimeoutMs", attemptTimeout ?? DefaultAttemptTimeout);
            if (attempt >= total) attempt = total - TimeSpan.FromMilliseconds(1);

            options.TotalRequestTimeout.Timeout = total;
            options.AttemptTimeout.Timeout = attempt;

            options.Retry.MaxRetryAttempts = Math.Max(1, configuration.GetValue<int?>("QoS:Downstream:MaxRetryAttempts") ?? MaxRetryAttempts);
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;
            // ShouldHandle must be set BEFORE DisableForUnsafeHttpMethods(), which wraps the current predicate;
            // setting it afterwards would silently re-enable retries of POST/PUT/PATCH/DELETE.
            options.Retry.ShouldHandle = args => ValueTask.FromResult(IsOwnTransientFailure(args.Outcome));
            // Never replay non-idempotent commands (POST/PUT/PATCH/DELETE) from the gateway.
            options.Retry.DisableForUnsafeHttpMethods();

            options.CircuitBreaker.ShouldHandle = args => ValueTask.FromResult(IsOwnTransientFailure(args.Outcome));
            options.CircuitBreaker.FailureRatio = BreakerFailureRatio;
            options.CircuitBreaker.MinimumThroughput =
                configuration.GetValue<int?>("QoS:Downstream:BreakerMinimumThroughput") ?? BreakerMinimumThroughput;
            options.CircuitBreaker.SamplingDuration = BreakerSamplingDuration;
            options.CircuitBreaker.BreakDuration =
                Read(configuration, "QoS:Downstream:BreakDurationMs", BreakerBreakDuration);
        });

        return builder;
    }

    private static TimeSpan Read(IConfiguration configuration, string key, TimeSpan fallback)
    {
        var ms = configuration.GetValue<int?>(key);
        return ms is > 0 ? TimeSpan.FromMilliseconds(ms.Value) : fallback;
    }
}
