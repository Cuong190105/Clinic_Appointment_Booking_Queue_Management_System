using System.Text;
using System.Text.Json;
using Clinic.MedicalRecords.Service.Services;
using RabbitMQ.Client;

namespace Clinic.MedicalRecords.Service.Tests.Services;

public sealed class MedicalRecordsRetryPolicyTests
{
    [Fact]
    public void RetryCount_ReadsRabbitMqHeaderFormats()
    {
        Assert.Equal(2, MedicalRecordsRetryPolicy.GetRetryCount(
            new Dictionary<string, object?> { ["x-medical-records-retry-count"] = 2 }));
        Assert.Equal(3, MedicalRecordsRetryPolicy.GetRetryCount(
            new Dictionary<string, object?> { ["x-medical-records-retry-count"] = Encoding.UTF8.GetBytes("3") }));
    }

    [Fact]
    public void RetryPolicy_SendsTransientFailuresThroughThreeBoundedDelays()
    {
        Assert.False(MedicalRecordsRetryPolicy.ShouldDeadLetter(new IOException(), 0));
        Assert.Equal("clinic.medical-records.retry.10s", MedicalRecordsRetryPolicy.GetRetryQueueName(0));
        Assert.Equal("clinic.medical-records.retry.1m", MedicalRecordsRetryPolicy.GetRetryQueueName(1));
        Assert.Equal("clinic.medical-records.retry.5m", MedicalRecordsRetryPolicy.GetRetryQueueName(2));
        Assert.True(MedicalRecordsRetryPolicy.ShouldDeadLetter(new IOException(), 3));
    }

    [Fact]
    public void RetryPolicy_DeadLettersMalformedEventsImmediately()
    {
        Assert.True(MedicalRecordsRetryPolicy.ShouldDeadLetter(new FormatException(), 0));
        Assert.True(MedicalRecordsRetryPolicy.ShouldDeadLetter(new JsonException(), 0));
    }

    [Fact]
    public void RetryProperties_PreserveTraceMetadataAndIncrementCount()
    {
        var source = new BasicProperties
        {
            MessageId = "message-id",
            CorrelationId = "correlation-id",
            Type = "event-type",
            ContentType = "application/json",
            Headers = new Dictionary<string, object?> { ["custom"] = "value" }
        };

        var retry = MedicalRecordsRetryPolicy.CreateRetryProperties(source, 1);

        Assert.Equal(source.MessageId, retry.MessageId);
        Assert.Equal(source.CorrelationId, retry.CorrelationId);
        Assert.Equal(source.Type, retry.Type);
        Assert.Equal(1, retry.Headers!["x-medical-records-retry-count"]);
        Assert.Equal("value", retry.Headers["custom"]);
    }
}
