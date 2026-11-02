using Clinic.Notification.Service.Services;
namespace Clinic.Notification.Service.Tests;
public sealed class DeliveryRetryPolicyTests
{
    [Fact] public void TransientFailuresUseBoundedExponentialBackoff()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(DeliveryRetryPolicy.ShouldRetry(true, 1)); Assert.False(DeliveryRetryPolicy.ShouldRetry(true, 5)); Assert.False(DeliveryRetryPolicy.ShouldRetry(false, 1));
        Assert.Equal(TimeSpan.FromSeconds(10), DeliveryRetryPolicy.NextAttempt(now, 1) - now);
        Assert.Equal(TimeSpan.FromSeconds(300), DeliveryRetryPolicy.NextAttempt(now, 10) - now);
    }
}
