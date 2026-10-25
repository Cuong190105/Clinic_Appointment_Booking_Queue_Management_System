using Clinic.Queue.Service.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Clinic.Queue.Service.Tests;

public class QueueMessagingTests
{
    [Fact]
    public async Task QueueOutboxPublisher_Respects_SkipBackgroundWorkers()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SkipBackgroundWorkers"] = "true"
            })
            .Build();

        var publisher = new QueueOutboxPublisher(services, config, NullLogger<QueueOutboxPublisher>.Instance);
        using var cts = new CancellationTokenSource();

        // Should return immediately without blocking or connecting to RabbitMQ
        await publisher.StartAsync(cts.Token);
        await publisher.StopAsync(cts.Token);
    }

    [Fact]
    public async Task QueueEventConsumer_Respects_SkipBackgroundWorkers()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SkipBackgroundWorkers"] = "true"
            })
            .Build();

        var consumer = new QueueEventConsumer(services, config, NullLogger<QueueEventConsumer>.Instance);
        using var cts = new CancellationTokenSource();

        // Should return immediately without blocking or connecting to RabbitMQ
        await consumer.StartAsync(cts.Token);
        await consumer.StopAsync(cts.Token);
    }

    [Fact]
    public async Task QueueOutboxPublisher_StopsGracefully_WhenCancelled()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SkipBackgroundWorkers"] = "false",
                ["RabbitMq:Host"] = "non-existent-host"
            })
            .Build();

        var publisher = new QueueOutboxPublisher(services, config, NullLogger<QueueOutboxPublisher>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // cancelled immediately

        await publisher.StartAsync(cts.Token);
        await publisher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task QueueEventConsumer_StopsGracefully_WhenCancelled()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SkipBackgroundWorkers"] = "false",
                ["RabbitMq:Host"] = "non-existent-host"
            })
            .Build();

        var consumer = new QueueEventConsumer(services, config, NullLogger<QueueEventConsumer>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // cancelled immediately

        await consumer.StartAsync(cts.Token);
        await consumer.StopAsync(CancellationToken.None);
    }
}

