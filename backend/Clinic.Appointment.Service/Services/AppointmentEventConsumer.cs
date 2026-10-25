namespace Clinic.Appointment.Service.Services;

using System.Text;
using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public sealed class AppointmentEventConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppointmentEventConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public AppointmentEventConsumer(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<AppointmentEventConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppointmentEventConsumer started.");

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
                _logger.LogWarning("AppointmentEventConsumer connection/loop warning: {Message}", ex.Message);
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
        _logger.LogInformation("AppointmentEventConsumer stopped.");
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
            ClientProvidedName = "clinic-appointment-event-consumer"
        };

        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        const string exchangeName = "clinic.events";
        const string queueName = "clinic.appointment.events";

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
            routingKey: "queue.exam-completed",
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: "identity.user-profile-updated",
            cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(args.Body.Span);
                await using var scope = _serviceProvider.CreateAsyncScope();
                var manager = scope.ServiceProvider.GetRequiredService<IAppointmentManager>();

                if (args.RoutingKey == "queue.exam-completed" || json.Contains(EventTypes.ExamCompleted))
                {
                    ExamCompleted? payload = null;
                    try
                    {
                        var envelope = JsonSerializer.Deserialize<EventEnvelope<ExamCompleted>>(
                            json,
                            EventJsonSerializerOptions.Default);
                        payload = envelope?.Payload;
                    }
                    catch
                    {
                        // Fallback to direct deserialization
                    }

                    if (payload == null)
                    {
                        try
                        {
                            payload = JsonSerializer.Deserialize<ExamCompleted>(
                                json,
                                EventJsonSerializerOptions.Default);
                        }
                        catch { }
                    }

                    if (payload != null)
                    {
                        await manager.ProcessExamCompletedAsync(payload, ct);
                        _logger.LogInformation("Processed ExamCompleted event for AppointmentId={AppointmentId}", payload.AppointmentId);
                    }
                }
                else if (args.RoutingKey == "identity.user-profile-updated" || json.Contains(EventTypes.UserProfileUpdated))
                {
                    var envelope = JsonSerializer.Deserialize<EventEnvelope<UserProfileUpdated>>(
                        json,
                        EventJsonSerializerOptions.Default);
                    var payload = envelope?.Payload ?? JsonSerializer.Deserialize<UserProfileUpdated>(
                        json,
                        EventJsonSerializerOptions.Default);

                    if (payload != null)
                    {
                        await manager.ProcessUserProfileUpdatedAsync(payload, ct);
                        _logger.LogInformation("Processed UserProfileUpdated event for PersonId={PersonId}", payload.PersonId);
                    }
                }

                if (_channel is { IsOpen: true })
                {
                    await _channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing appointment event message with RoutingKey={RoutingKey}", args.RoutingKey);
                if (_channel is { IsOpen: true })
                {
                    await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
                }
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }

    private async Task DisconnectAsync()
    {
        if (_channel != null)
        {
            try { await _channel.CloseAsync(); } catch { }
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection != null)
        {
            try { await _connection.CloseAsync(); } catch { }
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}

