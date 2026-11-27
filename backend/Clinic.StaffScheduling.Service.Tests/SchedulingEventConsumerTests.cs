using System.Text;
using System.Text.Json;
using Clinic.StaffScheduling.Service.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace Clinic.StaffScheduling.Service.Tests;

public class SchedulingEventConsumerTests
{
    [Fact]
    public async Task SchedulingEventConsumer_Respects_SkipBackgroundWorkers()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SkipBackgroundWorkers"] = "true"
            })
            .Build();

        var consumer = new SchedulingEventConsumer(services, config, NullLogger<SchedulingEventConsumer>.Instance);
        using var cts = new CancellationTokenSource();

        await consumer.StartAsync(cts.Token);
        await consumer.StopAsync(cts.Token);
    }

    [Fact]
    public async Task SchedulingEventConsumer_StopsGracefully_WhenCancelled()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SkipBackgroundWorkers"] = "false",
                ["RabbitMq:Host"] = "non-existent-host"
            })
            .Build();

        var consumer = new SchedulingEventConsumer(services, config, NullLogger<SchedulingEventConsumer>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await consumer.StartAsync(cts.Token);
        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void RetryPolicy_GetRetryCount_ParsesNumericAndByteHeaders()
    {
        Assert.Equal(0, SchedulingRetryPolicy.GetRetryCount(null));
        Assert.Equal(0, SchedulingRetryPolicy.GetRetryCount(new Dictionary<string, object?>()));
        Assert.Equal(0, SchedulingRetryPolicy.GetRetryCount(new Dictionary<string, object?>
        {
            [SchedulingRetryPolicy.RetryCountHeader] = Encoding.UTF8.GetBytes("invalid")
        }));
        Assert.Equal(2, SchedulingRetryPolicy.GetRetryCount(new Dictionary<string, object?>
        {
            [SchedulingRetryPolicy.RetryCountHeader] = 2
        }));
        Assert.Equal(3, SchedulingRetryPolicy.GetRetryCount(new Dictionary<string, object?>
        {
            [SchedulingRetryPolicy.RetryCountHeader] = Encoding.UTF8.GetBytes("3")
        }));
    }

    [Fact]
    public void RetryPolicy_ShouldDeadLetter_OnMalformedOrMaxRetriesOnly()
    {
        Assert.True(SchedulingRetryPolicy.ShouldDeadLetter(new JsonException("bad json"), 0));
        Assert.True(SchedulingRetryPolicy.ShouldDeadLetter(new FormatException("bad format"), 0));
        Assert.False(SchedulingRetryPolicy.ShouldDeadLetter(new InvalidOperationException("transient db"), 0));
        Assert.False(SchedulingRetryPolicy.ShouldDeadLetter(new InvalidOperationException("transient db"), 2));
        Assert.True(SchedulingRetryPolicy.ShouldDeadLetter(new InvalidOperationException("transient db"), SchedulingRetryPolicy.MaximumRetries));
    }

    [Fact]
    public void RetryPolicy_GetRetryQueueName_FollowsStaggeredBackoff()
    {
        Assert.Equal("clinic.scheduling.retry.5s", SchedulingRetryPolicy.GetRetryQueueName(0));
        Assert.Equal("clinic.scheduling.retry.30s", SchedulingRetryPolicy.GetRetryQueueName(1));
        Assert.Equal("clinic.scheduling.retry.2m", SchedulingRetryPolicy.GetRetryQueueName(2));
        Assert.Equal("clinic.scheduling.retry.2m", SchedulingRetryPolicy.GetRetryQueueName(99));
    }

    [Fact]
    public void RetryPolicy_GetEffectiveRoutingKey_And_CreateRetryProperties_PreserveOriginalMetadata()
    {
        var source = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = "msg-789",
            CorrelationId = "corr-012",
            Type = "appointment.cancelled"
        };

        var retryProps = SchedulingRetryPolicy.CreateRetryProperties(source, "appointment.cancelled", 1);

        Assert.True(retryProps.Persistent);
        Assert.Equal("application/json", retryProps.ContentType);
        Assert.Equal("msg-789", retryProps.MessageId);
        Assert.Equal("corr-012", retryProps.CorrelationId);
        Assert.Equal("appointment.cancelled", retryProps.Type);
        Assert.Equal(1, SchedulingRetryPolicy.GetRetryCount(retryProps.Headers));
        Assert.Equal(
            "appointment.cancelled",
            SchedulingRetryPolicy.GetEffectiveRoutingKey(SchedulingRetryPolicy.RetryRoutingKey, retryProps.Headers));
        Assert.Equal(
            "identity.user-profile-updated",
            SchedulingRetryPolicy.GetEffectiveRoutingKey("identity.user-profile-updated", null));
        Assert.Equal(
            "appointment.cancelled",
            SchedulingRetryPolicy.GetEffectiveRoutingKey(
                SchedulingRetryPolicy.RetryRoutingKey,
                new Dictionary<string, object?>
                {
                    [SchedulingRetryPolicy.OriginalRoutingKeyHeader] = Encoding.UTF8.GetBytes("appointment.cancelled")
                }));
    }
}
