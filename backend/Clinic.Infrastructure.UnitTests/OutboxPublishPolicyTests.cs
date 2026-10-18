using Clinic.Infrastructure.Sqlserver.Outbox;

namespace Clinic.Infrastructure.UnitTests;

public sealed class OutboxPublishPolicyTests
{
    [Fact]
    public void First_failure_is_retried_after_ten_seconds()
    {
        var now = DateTimeOffset.UtcNow;

        var next = OutboxPublishPolicy.GetNextAttemptAt(1, 3, now);

        Assert.Equal(now.AddSeconds(10), next);
    }

    [Fact]
    public void Second_failure_is_retried_after_one_minute()
    {
        var now = DateTimeOffset.UtcNow;

        var next = OutboxPublishPolicy.GetNextAttemptAt(2, 3, now);

        Assert.Equal(now.AddMinutes(1), next);
    }

    [Fact]
    public void Max_retries_has_no_further_normal_retry_time()
    {
        var next = OutboxPublishPolicy.GetNextAttemptAt(3, 3, DateTimeOffset.UtcNow);

        Assert.Null(next);
    }

    [Fact]
    public void Reaching_max_retries_routes_message_to_dead_letter()
    {
        Assert.True(OutboxPublishPolicy.ShouldDeadLetter(3, 3));
        Assert.False(OutboxPublishPolicy.ShouldDeadLetter(2, 3));
    }

    [Fact]
    public void Broker_outage_keeps_outbox_message_scheduled_for_recovery()
    {
        var failedAt = DateTimeOffset.UtcNow;

        var retryAt = OutboxPublishPolicy.GetNextAttemptAt(1, 3, failedAt);

        Assert.Equal(failedAt.AddSeconds(10), retryAt);
        Assert.False(OutboxPublishPolicy.ShouldDeadLetter(1, 3));
    }
}
