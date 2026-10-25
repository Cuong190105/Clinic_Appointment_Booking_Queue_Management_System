using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Clinic.Queue.Service.Common;

public static class QueueTelemetry
{
    public const string MeterName = "Clinic.Queue";
    public const string ActivitySourceName = "Clinic.Queue";
    public static readonly Meter Meter = new(MeterName, "1.0.0");
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
    public static readonly Counter<long> TicketsCreated = Meter.CreateCounter<long>("clinic.queue.tickets.created");
    public static readonly Counter<long> ConsumerProcessed = Meter.CreateCounter<long>("clinic.queue.consumer.processed");
    public static readonly Counter<long> ConsumerFailures = Meter.CreateCounter<long>("clinic.queue.consumer.failures");
    public static readonly Counter<long> ConsumerRetries = Meter.CreateCounter<long>("clinic.queue.consumer.retries");
    public static readonly Counter<long> ConsumerDeadLettered = Meter.CreateCounter<long>("clinic.queue.consumer.dead_lettered");
    public static readonly Counter<long> OutboxPublished = Meter.CreateCounter<long>("clinic.queue.outbox.published");
    public static readonly Counter<long> OutboxFailed = Meter.CreateCounter<long>("clinic.queue.outbox.failures");
    public static readonly Counter<long> OutboxRetries = Meter.CreateCounter<long>("clinic.queue.outbox.retries");
    public static readonly Counter<long> OutboxDeadLettered = Meter.CreateCounter<long>("clinic.queue.outbox.dead_lettered");
    public static readonly Histogram<double> ConsumerLag = Meter.CreateHistogram<double>("clinic.queue.consumer.lag", "s");
    public static readonly Histogram<double> TicketProcessingTime = Meter.CreateHistogram<double>("clinic.queue.ticket.processing", "s");
    public static readonly Histogram<double> WaitTime = Meter.CreateHistogram<double>("clinic.queue.wait.time", "s");

    private static long _waitingTickets;
    private static long _pendingOutbox;
    private static long _oldestOutboxAgeSeconds;

    static QueueTelemetry()
    {
        Meter.CreateObservableGauge("clinic.queue.length", () => Interlocked.Read(ref _waitingTickets));
        Meter.CreateObservableGauge("clinic.queue.outbox.pending", () => Interlocked.Read(ref _pendingOutbox));
        Meter.CreateObservableGauge("clinic.queue.outbox.oldest.pending.age", () => Interlocked.Read(ref _oldestOutboxAgeSeconds), "s");
    }

    public static void UpdateSnapshot(long waitingTickets, long pendingOutbox, long oldestOutboxAgeSeconds)
    {
        Interlocked.Exchange(ref _waitingTickets, waitingTickets);
        Interlocked.Exchange(ref _pendingOutbox, pendingOutbox);
        Interlocked.Exchange(ref _oldestOutboxAgeSeconds, oldestOutboxAgeSeconds);
    }
}
