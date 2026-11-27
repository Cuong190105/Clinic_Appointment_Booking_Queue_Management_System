using System.Diagnostics.Metrics;
using System.Threading.RateLimiting;

namespace Clinic.Identity.Service.Auth;

/// <summary>Thrown when every password-hashing slot is busy and the wait queue is full.</summary>
public sealed class PasswordHashingOverloadedException() : Exception("Password hashing capacity is exhausted.");

/// <summary>
/// BCrypt is CPU-bound by design, so login/register throughput is bounded by Identity's CPU
/// (SLI_SLO_MATRIX §2.3 "Identity capacity"). This service:
/// <list type="bullet">
/// <item>hashes with a configurable work factor (<c>Security:BcryptWorkFactor</c>, default 10 = OWASP minimum);</item>
/// <item>reports hashes stored with a different factor so login can transparently re-hash them;</item>
/// <item>bounds concurrent hashing with a small queue. When the queue is full the caller gets an
/// immediate 503 + Retry-After (load shedding) instead of piling up until the 9 s gateway deadline (504).</item>
/// </list>
/// </summary>
public sealed class PasswordHashing : IDisposable
{
    public const int DefaultWorkFactor = 10;
    public const int MinimumWorkFactor = 4;   // low values are only for tests; production stays >= 10
    public const int MaximumWorkFactor = 16;
    public const string LoadShedHeader = "X-Clinic-Load-Shed";
    public const string LoadShedReason = "password-hashing";
    public const int RetryAfterSeconds = 1;
    public const string MeterName = "Clinic.Identity.QoS";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Rejected = Meter.CreateCounter<long>(
        "clinic_identity_password_hash_rejected", description: "Password hash operations shed because capacity was exhausted.");

    private readonly ConcurrencyLimiter _limiter;

    public PasswordHashing(IConfiguration configuration)
    {
        WorkFactor = Math.Clamp(
            configuration.GetValue<int?>("Security:BcryptWorkFactor") ?? DefaultWorkFactor, MinimumWorkFactor, MaximumWorkFactor);
        // One BCrypt computation saturates one core. Leave one core free (bulkhead) so request handling,
        // token refresh and the fast 503 path are not starved when hashing is saturated (measured with
        // login-capacity.js: 2 permits on 1 CPU pushed even rejections to p95 1.3 s).
        PermitLimit = Math.Max(1, configuration.GetValue<int?>("QoS:PasswordHashConcurrency") ?? Environment.ProcessorCount - 1);
        // Short queue: at ~10 hashes/s, 4 waiting ≈ 0.4 s, which keeps accepted logins within the 1 s SLO.
        QueueLimit = Math.Max(0, configuration.GetValue<int?>("QoS:PasswordHashQueueLimit") ?? 4);
        _limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = PermitLimit,
            QueueLimit = QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    }

    public int WorkFactor { get; }
    public int PermitLimit { get; }
    public int QueueLimit { get; }

    public async ValueTask<bool> VerifyAsync(string password, string passwordHash, CancellationToken ct)
    {
        using var lease = await AcquireAsync(ct);
        try { return BCrypt.Net.BCrypt.Verify(password, passwordHash); }
        catch (BCrypt.Net.SaltParseException) { return false; }
    }

    public async ValueTask<string> HashAsync(string password, CancellationToken ct)
    {
        using var lease = await AcquireAsync(ct);
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    /// <summary>True when the stored hash uses a different work factor than the configured one.</summary>
    public bool NeedsRehash(string passwordHash) => WorkFactorOf(passwordHash) is { } cost && cost != WorkFactor;

    /// <summary>Reads the cost from a modular-crypt BCrypt hash such as <c>$2a$11$...</c>.</summary>
    public static int? WorkFactorOf(string? passwordHash) =>
        passwordHash is { Length: >= 7 } && passwordHash[0] == '$' && passwordHash[3] == '$' && passwordHash[6] == '$' &&
        int.TryParse(passwordHash.AsSpan(4, 2), out var cost)
            ? cost
            : null;

    private async ValueTask<RateLimitLease> AcquireAsync(CancellationToken ct)
    {
        var lease = await _limiter.AcquireAsync(1, ct);
        if (lease.IsAcquired) return lease;
        lease.Dispose();
        Rejected.Add(1);
        throw new PasswordHashingOverloadedException();
    }

    public void Dispose() => _limiter.Dispose();
}
