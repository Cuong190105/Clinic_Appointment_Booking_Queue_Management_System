using Clinic.Application.Interfaces;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;
using Clinic.Infrastructure.Sqlserver.Inbox;
using Clinic.Infrastructure.Sqlserver.Outbox;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Clinic.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Clinic.IntegrationTests.Messaging;

public sealed class RabbitMqFailureIntegrationTests : IClassFixture<ApiContractFactory>, IAsyncLifetime
{
    private const string ConsumerName = "integration-tests.appointment-checked-in";
    private readonly ApiContractFactory _factory;
    private readonly ConnectionFactory _rabbitConnectionFactory;

    public RabbitMqFailureIntegrationTests(ApiContractFactory factory)
    {
        _factory = factory;
        var appSettingsPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Clinic.API", "appsettings.json"));
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(appSettingsPath, optional: false)
            .AddEnvironmentVariables()
            .Build();
        var options = configuration.GetSection("RabbitMq").Get<RabbitMqOptions>()
            ?? throw new InvalidOperationException("RabbitMq configuration is required for broker integration tests.");

        _rabbitConnectionFactory = new ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            UserName = options.Username,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            ClientProvidedName = "clinic-rabbitmq-failure-integration-tests"
        };
    }

    public async Task InitializeAsync() => await _factory.InitializeDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Broker_redelivery_after_consumer_crash_is_deduplicated_by_sql_inbox()
    {
        var queueName = $"clinic.integration.crash.{Guid.NewGuid():N}";
        await using var connection = await _rabbitConnectionFactory.CreateConnectionAsync();
        try
        {
            var envelope = await CreateCheckedInEnvelopeAsync();
            var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                envelope,
                EventJsonSerializerOptions.Default);

            await using (var publishChannel = await connection.CreateChannelAsync())
            {
                await publishChannel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);
                await publishChannel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: queueName,
                    mandatory: true,
                    basicProperties: new BasicProperties
                    {
                        Persistent = true,
                        MessageId = envelope.EventId.ToString(),
                        ContentType = "application/json"
                    },
                    body: payload);
            }

            IChannel crashedConsumerChannel = await connection.CreateChannelAsync();
            var firstDelivery = await crashedConsumerChannel.BasicGetAsync(queueName, autoAck: false);
            Assert.NotNull(firstDelivery);
            Assert.Equal(
                AppointmentCheckedInProcessingResult.Processed,
                await ProcessAsync(envelope));

            // Simulate a consumer process crashing after its database commit but before ACK.
            await crashedConsumerChannel.DisposeAsync();

            await using var restartedConsumerChannel = await connection.CreateChannelAsync();
            var redelivery = await restartedConsumerChannel.BasicGetAsync(queueName, autoAck: false);
            Assert.NotNull(redelivery);
            Assert.True(redelivery.Redelivered);
            Assert.Equal(envelope.EventId.ToString(), redelivery.BasicProperties.MessageId);

            Assert.Equal(
                AppointmentCheckedInProcessingResult.AlreadyProcessed,
                await ProcessAsync(envelope));
            await restartedConsumerChannel.BasicAckAsync(redelivery.DeliveryTag, multiple: false);

            await using var dbContext = _factory.CreateDbContext();
            var inboxRows = await dbContext.InboxMessages
                .Where(message => message.EventId == envelope.EventId && message.ConsumerName == ConsumerName)
                .ToListAsync();
            Assert.Single(inboxRows);
            Assert.Equal(InboxMessageStatus.Processed, inboxRows[0].Status);
        }
        finally
        {
            await using var cleanupChannel = await connection.CreateChannelAsync();
            await cleanupChannel.QueueDeleteAsync(queueName);
        }
    }

    [Fact]
    public async Task Out_of_order_broker_message_is_retried_without_committing_inbox_as_processed()
    {
        var queueName = $"clinic.integration.out-of-order.{Guid.NewGuid():N}";
        await using var connection = await _rabbitConnectionFactory.CreateConnectionAsync();
        try
        {
            var envelope = CreateEnvelopeForMissingAppointment();
            var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                envelope,
                EventJsonSerializerOptions.Default);

            await using var channel = await connection.CreateChannelAsync();
            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: true,
                basicProperties: new BasicProperties
                {
                    Persistent = true,
                    MessageId = envelope.EventId.ToString(),
                    ContentType = "application/json"
                },
                body: payload);

            var firstDelivery = await channel.BasicGetAsync(queueName, autoAck: false);
            Assert.NotNull(firstDelivery);
            await Assert.ThrowsAsync<InvalidOperationException>(() => ProcessAsync(envelope));
            await channel.BasicNackAsync(firstDelivery.DeliveryTag, multiple: false, requeue: true);

            var retriedDelivery = await channel.BasicGetAsync(queueName, autoAck: false);
            Assert.NotNull(retriedDelivery);
            Assert.True(retriedDelivery.Redelivered);
            await Assert.ThrowsAsync<InvalidOperationException>(() => ProcessAsync(envelope));
            await channel.BasicNackAsync(retriedDelivery.DeliveryTag, multiple: false, requeue: false);

            await using var dbContext = _factory.CreateDbContext();
            Assert.False(await dbContext.InboxMessages.AnyAsync(message => message.EventId == envelope.EventId));
        }
        finally
        {
            await using var cleanupChannel = await connection.CreateChannelAsync();
            await cleanupChannel.QueueDeleteAsync(queueName);
        }
    }

    private async Task<AppointmentCheckedInProcessingResult> ProcessAsync(
        EventEnvelope<AppointmentCheckedIn> envelope)
    {
        await using var dbContext = _factory.CreateDbContext();
        return await new AppointmentCheckedInProcessor(
            dbContext,
            new InboxStore(dbContext),
            ConsumerName).ProcessAsync(envelope, CancellationToken.None);
    }

    private async Task<EventEnvelope<AppointmentCheckedIn>> CreateCheckedInEnvelopeAsync()
    {
        await using var dbContext = _factory.CreateDbContext();
        var appointment = await dbContext.Appointments
            .Include(item => item.WorkSchedule)
            .Include(item => item.QueueTicket)
            .FirstAsync(item => !item.IsDeleted && item.QueueTicket != null);

        return new EventEnvelope<AppointmentCheckedIn>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.AppointmentCheckedIn,
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Producer = "integration-test",
            Payload = new AppointmentCheckedIn(
                appointment.Id,
                appointment.PatientId,
                appointment.WorkSchedule.DoctorId,
                appointment.Id,
                DateTimeOffset.UtcNow)
        };
    }

    private static EventEnvelope<AppointmentCheckedIn> CreateEnvelopeForMissingAppointment()
    {
        var now = DateTimeOffset.UtcNow;
        return new EventEnvelope<AppointmentCheckedIn>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.AppointmentCheckedIn,
            SchemaVersion = 1,
            OccurredAt = now,
            CorrelationId = Guid.NewGuid(),
            Producer = "integration-test",
            Payload = new AppointmentCheckedIn(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                now)
        };
    }
}
