namespace Clinic.Scheduling.Service.Tests;

using Clinic.Scheduling.Service.Persistence;
using Clinic.Scheduling.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class SchedulingOutboxPublisherTests
{
    private static (IServiceProvider ServiceProvider, SchedulingDbContext DbContext) CreateServices()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();

        services.AddDbContext<SchedulingDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<SchedulingDbContext>();

        return (sp, db);
    }

    [Fact]
    public async Task PublishPendingOutboxMessagesAsync_WhenNoMessages_ReturnsZeroWithoutConnecting()
    {
        var (serviceProvider, _) = CreateServices();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:Host"] = "non-existent-host",
            ["RabbitMq:Port"] = "9999"
        }).Build();

        var publisher = new SchedulingOutboxPublisher(
            serviceProvider,
            config,
            NullLogger<SchedulingOutboxPublisher>.Instance);

        var result = await publisher.PublishPendingOutboxMessagesAsync(CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSkipBackgroundWorkersIsTrue_TerminatesImmediately()
    {
        var (serviceProvider, _) = CreateServices();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SkipBackgroundWorkers"] = "true"
        }).Build();

        var publisher = new SchedulingOutboxPublisher(
            serviceProvider,
            config,
            NullLogger<SchedulingOutboxPublisher>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await publisher.StartAsync(cts.Token);
        await publisher.StopAsync(cts.Token);

        Assert.False(cts.IsCancellationRequested);
    }
}

