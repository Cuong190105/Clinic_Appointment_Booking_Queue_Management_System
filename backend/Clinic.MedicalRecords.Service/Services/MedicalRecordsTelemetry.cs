using System.Diagnostics.Metrics;
namespace Clinic.MedicalRecords.Service.Services;
public static class MedicalRecordsTelemetry
{
    public const string MeterName = "Clinic.MedicalRecords";
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> Processed = Meter.CreateCounter<long>("medical_records.consumer.processed");
    public static readonly Counter<long> Duplicates = Meter.CreateCounter<long>("medical_records.consumer.duplicates");
    public static readonly Counter<long> Failures = Meter.CreateCounter<long>("medical_records.consumer.failures");
    public static readonly Histogram<double> Lag = Meter.CreateHistogram<double>("medical_records.consumer.lag", "s");
}
