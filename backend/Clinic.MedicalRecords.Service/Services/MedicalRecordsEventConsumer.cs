using System.Text;
using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;
using Clinic.MedicalRecords.Service.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Clinic.MedicalRecords.Service.Services;

public sealed class MedicalRecordsEventConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MedicalRecordsEventConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public MedicalRecordsEventConsumer(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<MedicalRecordsEventConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MedicalRecordsEventConsumer started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("MedicalRecordsEventConsumer connection/loop warning: {Message}", ex.Message);
                await DisconnectAsync();

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        await DisconnectAsync();
        _logger.LogInformation("MedicalRecordsEventConsumer stopped.");
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        var host = _configuration["RabbitMq:Host"] ?? _configuration["RabbitMQ__Host"] ?? "rabbitmq";
        var portStr = _configuration["RabbitMq:Port"] ?? _configuration["RabbitMQ__Port"] ?? "5672";
        int.TryParse(portStr, out var port);
        if (port <= 0) port = 5672;

        var username = _configuration["RabbitMq:Username"] ?? _configuration["RabbitMQ__Username"] ?? "clinic";
        var password = _configuration["RabbitMq:Password"] ?? _configuration["RabbitMQ__Password"] ?? "clinic-dev-password";

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = username,
            Password = password,
            ClientProvidedName = "clinic-medical-records-event-consumer"
        };

        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        const string exchangeName = "clinic.events";
        const string queueName = "clinic.medical-records.events";

        await _channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: "identity.user-profile-updated",
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: "queue.exam-started",
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: "queue.exam-completed",
            cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(args.Body.Span);
                await using var scope = _serviceProvider.CreateAsyncScope();
                var manager = scope.ServiceProvider.GetRequiredService<IMedicalRecordsManager>();

                if (args.RoutingKey == "identity.user-profile-updated" || json.Contains(EventTypes.UserProfileUpdated))
                {
                    UserProfileUpdated? payload = null;
                    try
                    {
                        var envelope = JsonSerializer.Deserialize<EventEnvelope<UserProfileUpdated>>(
                            json,
                            EventJsonSerializerOptions.Default);
                        payload = envelope?.Payload;
                    }
                    catch { }

                    payload ??= JsonSerializer.Deserialize<UserProfileUpdated>(json, EventJsonSerializerOptions.Default);

                    if (payload != null)
                    {
                        await manager.HandleUserProfileUpdatedAsync(payload, ct);
                        _logger.LogInformation("Processed UserProfileUpdated event for PersonId={PersonId}", payload.PersonId);
                    }
                }

                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event from {RoutingKey}", args.RoutingKey);
                try
                {
                    await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, ct);
                }
                catch { }
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        _logger.LogInformation("MedicalRecordsEventConsumer connected and subscribed to {QueueName}", queueName);

        var tcs = new TaskCompletionSource();
        using (ct.Register(() => tcs.TrySetResult()))
        {
            await tcs.Task;
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            if (_channel != null)
            {
                await _channel.CloseAsync();
                _channel.Dispose();
                _channel = null;
            }
            if (_connection != null)
            {
                await _connection.CloseAsync();
                _connection.Dispose();
                _connection = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error closing RabbitMQ connection/channel.");
        }
    }
}
