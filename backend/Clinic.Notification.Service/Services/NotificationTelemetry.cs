using System.Diagnostics.Metrics;
namespace Clinic.Notification.Service.Services;
public static class NotificationTelemetry
{
    public const string MeterName = "Clinic.Notification";
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> Events = Meter.CreateCounter<long>("notification.consumer.processed");
    public static readonly Counter<long> Duplicates = Meter.CreateCounter<long>("notification.consumer.duplicates");
    public static readonly Counter<long> Sent = Meter.CreateCounter<long>("notification.delivery.sent");
    public static readonly Counter<long> Retried = Meter.CreateCounter<long>("notification.delivery.retried");
    public static readonly Counter<long> DeadLettered = Meter.CreateCounter<long>("notification.delivery.dead_lettered");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("notification.delivery.duration", "ms");
}
