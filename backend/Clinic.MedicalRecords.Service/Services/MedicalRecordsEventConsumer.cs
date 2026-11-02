using System.Text;
using System.Text.Json;
using Clinic.Contracts.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Clinic.MedicalRecords.Service.Services;

public sealed class MedicalRecordsEventConsumer(IServiceScopeFactory scopes, IConfiguration config,
    ILogger<MedicalRecordsEventConsumer> logger) : BackgroundService
{
    private IConnection? connection;
    private IChannel? channel;
    private const string QueueName = "q.medical-records.events";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (config.GetValue<bool>("SkipBackgroundWorkers")) return;
        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMq:Host"] ?? "rabbitmq",
            Port = config.GetValue("RabbitMq:Port", 5672),
            UserName = config["RabbitMq:Username"] ?? "clinic",
            Password = config["RabbitMq:Password"] ?? "clinic-dev-password",
            VirtualHost = config["RabbitMq:VirtualHost"] ?? "/",
            ClientProvidedName = "clinic-medical-records-consumer",
            AutomaticRecoveryEnabled = true
        };
        while (!ct.IsCancellationRequested)
        {
            try
            {
                connection = await factory.CreateConnectionAsync(ct);
                channel = await connection.CreateChannelAsync(cancellationToken: ct);
                await DeclareTopologyAsync(channel, ct);
                await channel.BasicQosAsync(0, 8, false, ct);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, args) => await ConsumeAsync(args, ct);
                await channel.BasicConsumeAsync(QueueName, false, consumer, ct);
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Medical records broker connection failed; reconnecting");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    private static async Task DeclareTopologyAsync(IChannel ch, CancellationToken ct)
    {
        await ch.ExchangeDeclareAsync("clinic.events", ExchangeType.Topic, true, false, cancellationToken: ct);
        await ch.ExchangeDeclareAsync("clinic.dead-letter", ExchangeType.Direct, true, false, cancellationToken: ct);
        await ch.QueueDeclareAsync(queue: QueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = "clinic.dead-letter", ["x-dead-letter-routing-key"] = "q.medical-records.dlq" }, cancellationToken: ct);
        await ch.QueueDeclareAsync("q.medical-records.dlq", true, false, false, cancellationToken: ct);
        await ch.QueueBindAsync(QueueName, "clinic.events", "queue.exam-started", cancellationToken: ct);
        await ch.QueueBindAsync(QueueName, "clinic.events", "queue.exam-completed", cancellationToken: ct);
        await ch.QueueBindAsync("q.medical-records.dlq", "clinic.dead-letter", "q.medical-records.dlq", cancellationToken: ct);
    }

    private async Task ConsumeAsync(BasicDeliverEventArgs args, CancellationToken ct)
    {
        try
        {
            var json = Encoding.UTF8.GetString(args.Body.Span);
            await using var scope = scopes.CreateAsyncScope();
            var workflow = scope.ServiceProvider.GetRequiredService<MedicalRecordWorkflow>();
            if (args.RoutingKey == "queue.exam-started")
            {
                var envelope = JsonSerializer.Deserialize<EventEnvelope<ExamStarted>>(json, JsonOptions())
                    ?? throw new JsonException("Invalid ExamStarted envelope");
                var handled = await workflow.HandleStartedAsync(envelope, ct);
                if (handled) MedicalRecordsTelemetry.Processed.Add(1); else MedicalRecordsTelemetry.Duplicates.Add(1);
                MedicalRecordsTelemetry.Lag.Record(Math.Max(0, (DateTimeOffset.UtcNow - envelope.OccurredAt).TotalSeconds));
            }
            else
            {
                var envelope = JsonSerializer.Deserialize<EventEnvelope<ExamCompleted>>(json, JsonOptions())
                    ?? throw new JsonException("Invalid ExamCompleted envelope");
                var handled = await workflow.HandleCompletedAsync(envelope, ct);
                if (handled) MedicalRecordsTelemetry.Processed.Add(1); else MedicalRecordsTelemetry.Duplicates.Add(1);
                MedicalRecordsTelemetry.Lag.Record(Math.Max(0, (DateTimeOffset.UtcNow - envelope.OccurredAt).TotalSeconds));
            }
            await channel!.BasicAckAsync(args.DeliveryTag, false, ct);
        }
        catch (Exception ex)
        {
            MedicalRecordsTelemetry.Failures.Add(1);
            var retry = HeaderInt(args.BasicProperties.Headers, "x-retry-count");
            logger.LogWarning(ex, "Medical event processing failed. Retry={Retry}", retry);
            if (retry >= 4)
            {
                await channel!.BasicNackAsync(args.DeliveryTag, false, false, ct);
                return;
            }
            var props = new BasicProperties
            {
                DeliveryMode = DeliveryModes.Persistent, ContentType = "application/json", MessageId = args.BasicProperties.MessageId,
                CorrelationId = args.BasicProperties.CorrelationId,
                Headers = new Dictionary<string, object?> { ["x-retry-count"] = retry + 1 }
            };
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retry)), ct);
            await channel!.BasicPublishAsync("clinic.events", args.RoutingKey, true, props, args.Body, ct);
            await channel.BasicAckAsync(args.DeliveryTag, false, ct);
        }
    }

    private static int HeaderInt(IDictionary<string, object?>? headers, string key) =>
        headers?.TryGetValue(key, out var value) == true ? value switch { int i => i, long l => (int)l, byte[] b when int.TryParse(Encoding.UTF8.GetString(b), out var i) => i, _ => 0 } : 0;
    private static JsonSerializerOptions JsonOptions() => new() { PropertyNameCaseInsensitive = true };
}
