using Clinic.Identity.Service.Auth;
using Microsoft.Extensions.Configuration;

namespace Clinic.Identity.Service.Tests;

public sealed class PasswordHashingTests
{
    private static PasswordHashing Create(int workFactor = 4, int concurrency = 1, int queue = 0) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:BcryptWorkFactor"] = workFactor.ToString(),
            ["QoS:PasswordHashConcurrency"] = concurrency.ToString(),
            ["QoS:PasswordHashQueueLimit"] = queue.ToString()
        }).Build());

    [Fact]
    public void Defaults_UseOwaspMinimumWorkFactor()
    {
        using var hashing = new PasswordHashing(new ConfigurationBuilder().Build());
        Assert.Equal(10, hashing.WorkFactor);
        Assert.Equal(Math.Max(1, Environment.ProcessorCount - 1), hashing.PermitLimit);
        Assert.Equal(4, hashing.QueueLimit);
    }

    [Fact]
    public async Task Hash_UsesConfiguredWorkFactor_AndVerifies()
    {
        using var hashing = Create(workFactor: 5);
        var hash = await hashing.HashAsync("Correct horse 1", CancellationToken.None);

        Assert.Equal(5, PasswordHashing.WorkFactorOf(hash));
        Assert.True(await hashing.VerifyAsync("Correct horse 1", hash, CancellationToken.None));
        Assert.False(await hashing.VerifyAsync("wrong password", hash, CancellationToken.None));
        Assert.False(hashing.NeedsRehash(hash));
    }

    [Fact]
    public void NeedsRehash_WhenStoredCostDiffers_InEitherDirection()
    {
        using var hashing = Create(workFactor: 10);
        Assert.True(hashing.NeedsRehash(BCrypt.Net.BCrypt.HashPassword("pw-123456", 4)));
        Assert.True(hashing.NeedsRehash("$2a$11$abcdefghijklmnopqrstuuJ8m3l0b6a5b8m7o4b9F2e7d1aQ6k1Ky"));
        Assert.False(hashing.NeedsRehash("not-a-bcrypt-hash"));
        Assert.Null(PasswordHashing.WorkFactorOf(null));
    }

    [Fact]
    public async Task Verify_ReturnsFalse_ForMalformedHash()
    {
        using var hashing = Create();
        Assert.False(await hashing.VerifyAsync("anything", "not-a-bcrypt-hash", CancellationToken.None));
    }

    [Fact]
    public async Task SaturatedCapacity_ShedsLoad_InsteadOfQueueingUnbounded()
    {
        // One slot and no queue: of two concurrent slow hashes one runs and the other is shed at once.
        using var hashing = Create(workFactor: 12, concurrency: 1, queue: 0);
        var attempts = Enumerable.Range(0, 2)
            .Select(_ => Task.Run(async () =>
            {
                try { await hashing.HashAsync("slow password", CancellationToken.None); return "ok"; }
                catch (PasswordHashingOverloadedException) { return "shed"; }
            }))
            .ToArray();

        var outcomes = await Task.WhenAll(attempts);
        Assert.Contains("ok", outcomes);
        Assert.Contains("shed", outcomes);
    }

    [Fact]
    public async Task QueuedRequests_WaitForASlot_WithinQueueLimit()
    {
        using var hashing = Create(workFactor: 4, concurrency: 1, queue: 8);
        var results = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(i => hashing.HashAsync($"password-{i}", CancellationToken.None).AsTask()));
        Assert.All(results, hash => Assert.Equal(4, PasswordHashing.WorkFactorOf(hash)));
    }
}
