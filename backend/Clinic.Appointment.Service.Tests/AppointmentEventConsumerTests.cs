using System.Text;
using System.Text.Json;
using Clinic.Appointment.Service.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace Clinic.Appointment.Service.Tests;

public class AppointmentEventConsumerTests
{
    [Fact]
    public async Task AppointmentEventConsumer_Respects_SkipBackgroundWorkers()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SkipBackgroundWorkers"] = "true"
            })
            .Build();

        var consumer = new AppointmentEventConsumer(services, config, NullLogger<AppointmentEventConsumer>.Instance);
        using var cts = new CancellationTokenSource();

        await consumer.StartAsync(cts.Token);
        await consumer.StopAsync(cts.Token);
    }

    [Fact]
    public async Task AppointmentEventConsumer_StopsGracefully_WhenCancelled()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SkipBackgroundWorkers"] = "false",
                ["RabbitMq:Host"] = "non-existent-host"
            })
            .Build();

        var consumer = new AppointmentEventConsumer(services, config, NullLogger<AppointmentEventConsumer>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await consumer.StartAsync(cts.Token);
        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void RetryPolicy_GetRetryCount_ParsesNumericAndByteHeaders()
    {
        Assert.Equal(0, AppointmentRetryPolicy.GetRetryCount(null));
        Assert.Equal(0, AppointmentRetryPolicy.GetRetryCount(new Dictionary<string, object?>()));
        Assert.Equal(0, AppointmentRetryPolicy.GetRetryCount(new Dictionary<string, object?>
        {
            [AppointmentRetryPolicy.RetryCountHeader] = Encoding.UTF8.GetBytes("bad")
        }));
        Assert.Equal(2, AppointmentRetryPolicy.GetRetryCount(new Dictionary<string, object?>
        {
            [AppointmentRetryPolicy.RetryCountHeader] = 2
        }));
        Assert.Equal(3, AppointmentRetryPolicy.GetRetryCount(new Dictionary<string, object?>
        {
            [AppointmentRetryPolicy.RetryCountHeader] = Encoding.UTF8.GetBytes("3")
        }));
    }

    [Fact]
    public void RetryPolicy_ShouldDeadLetter_OnMalformedOrMaxRetriesOnly()
    {
        Assert.True(AppointmentRetryPolicy.ShouldDeadLetter(new JsonException("bad json"), 0));
        Assert.True(AppointmentRetryPolicy.ShouldDeadLetter(new FormatException("bad format"), 0));
        Assert.False(AppointmentRetryPolicy.ShouldDeadLetter(new InvalidOperationException("transient db"), 0));
        Assert.False(AppointmentRetryPolicy.ShouldDeadLetter(new InvalidOperationException("transient db"), 2));
        Assert.True(AppointmentRetryPolicy.ShouldDeadLetter(new InvalidOperationException("transient db"), AppointmentRetryPolicy.MaximumRetries));
    }

    [Fact]
    public void RetryPolicy_GetRetryQueueName_FollowsStaggeredBackoff()
    {
        Assert.Equal("clinic.appointment.retry.5s", AppointmentRetryPolicy.GetRetryQueueName(0));
        Assert.Equal("clinic.appointment.retry.30s", AppointmentRetryPolicy.GetRetryQueueName(1));
        Assert.Equal("clinic.appointment.retry.2m", AppointmentRetryPolicy.GetRetryQueueName(2));
        Assert.Equal("clinic.appointment.retry.2m", AppointmentRetryPolicy.GetRetryQueueName(99));
    }

    [Fact]
    public void RetryPolicy_GetEffectiveRoutingKey_And_CreateRetryProperties_PreserveOriginalMetadata()
    {
        var source = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = "msg-123",
            CorrelationId = "corr-456",
            Type = "queue.exam-completed"
        };

        var retryProps = AppointmentRetryPolicy.CreateRetryProperties(source, "queue.exam-completed", 1);

        Assert.True(retryProps.Persistent);
        Assert.Equal("application/json", retryProps.ContentType);
        Assert.Equal("msg-123", retryProps.MessageId);
        Assert.Equal("corr-456", retryProps.CorrelationId);
        Assert.Equal("queue.exam-completed", retryProps.Type);
        Assert.Equal(1, AppointmentRetryPolicy.GetRetryCount(retryProps.Headers));
        Assert.Equal(
            "queue.exam-completed",
            AppointmentRetryPolicy.GetEffectiveRoutingKey(AppointmentRetryPolicy.RetryRoutingKey, retryProps.Headers));
        Assert.Equal(
            "identity.user-profile-updated",
            AppointmentRetryPolicy.GetEffectiveRoutingKey("identity.user-profile-updated", null));
        Assert.Equal(
            "queue.exam-completed",
            AppointmentRetryPolicy.GetEffectiveRoutingKey(
                AppointmentRetryPolicy.RetryRoutingKey,
                new Dictionary<string, object?>
                {
                    [AppointmentRetryPolicy.OriginalRoutingKeyHeader] = Encoding.UTF8.GetBytes("queue.exam-completed")
                }));
    }
}
