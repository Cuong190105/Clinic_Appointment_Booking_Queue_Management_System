using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Clinic.Infrastructure.Sqlserver.Outbox;

public static class MessagingTelemetry
{
    public const string SourceName = "Clinic.Messaging";
    public const string MeterName = "Clinic.Messaging";

    public static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");
    public static readonly Meter Meter = new(MeterName, "1.0.0");
    public static readonly Counter<long> OutboxPublished = Meter.CreateCounter<long>("clinic.outbox.published");
    public static readonly Counter<long> OutboxPublishFailures = Meter.CreateCounter<long>("clinic.outbox.publish.failures");
    public static readonly Counter<long> OutboxPublishRetries = Meter.CreateCounter<long>("clinic.outbox.publish.retries");
    public static readonly Counter<long> OutboxDeadLettered = Meter.CreateCounter<long>("clinic.outbox.dead_lettered");
    public static readonly Counter<long> ConsumerProcessed = Meter.CreateCounter<long>("clinic.consumer.processed");
    public static readonly Counter<long> ConsumerDuplicates = Meter.CreateCounter<long>("clinic.consumer.duplicates");
    public static readonly Counter<long> ConsumerFailures = Meter.CreateCounter<long>("clinic.consumer.failures");
    public static readonly Counter<long> ConsumerRetries = Meter.CreateCounter<long>("clinic.consumer.retries");
    public static readonly Counter<long> ConsumerDeadLettered = Meter.CreateCounter<long>("clinic.consumer.dead_lettered");
    public static readonly Histogram<double> ConsumerLag = Meter.CreateHistogram<double>("clinic.consumer.lag", "s");

    private static readonly ActivityListener Listener = CreateListener();

    private static ActivityListener CreateListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    public static Activity? StartConsumerActivity(string? traceParent, string? traceState)
    {
        _ = Listener;
        var parent = ActivityContext.TryParse(traceParent, traceState, out var context)
            ? context
            : default;
        return ActivitySource.StartActivity("messaging.consume", ActivityKind.Consumer, parent);
    }
}
