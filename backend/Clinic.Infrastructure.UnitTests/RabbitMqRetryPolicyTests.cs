using System.Text;
using Clinic.Infrastructure.Sqlserver.Outbox;
using RabbitMQ.Client;

namespace Clinic.Infrastructure.UnitTests;

public sealed class RabbitMqRetryPolicyTests
{
    [Fact]
    public void Missing_retry_header_starts_at_zero()
    {
        var properties = new BasicProperties();

        Assert.Equal(0, RabbitMqRetryPolicy.GetAttempt(properties));
    }

    [Fact]
    public void Byte_retry_header_is_read_correctly()
    {
        var properties = new BasicProperties
        {
            Headers = new Dictionary<string, object?>
            {
                ["x-retry-attempt"] = Encoding.UTF8.GetBytes("2")
            }
        };

        Assert.Equal(2, RabbitMqRetryPolicy.GetAttempt(properties));
    }

    [Theory]
    [InlineData(1, "clinic.retry.10s")]
    [InlineData(2, "clinic.retry.1m")]
    [InlineData(3, "clinic.retry.5m")]
    public void Retry_attempt_maps_to_expected_exchange(int attempt, string exchange)
    {
        Assert.Equal(exchange, RabbitMqRetryPolicy.GetRetryExchange(attempt));
    }
}
