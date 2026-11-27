using System.Net;
using Clinic.Appointment.Service.Common;
using Clinic.Appointment.Service.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clinic.Appointment.Service.Tests;

/// <summary>
/// SLI_SLO_MATRIX §5.2 / §5.4: Scheduling dependency failures map to explicit QoS statuses,
/// and compensating releases never fail the caller.
/// </summary>
public class SchedulingDependencyQoSTests
{
    [Fact]
    public void CircuitOpen_MapsTo503_WithRetryAfter()
    {
        Assert.True(DownstreamFailure.TryMap(new Polly.CircuitBreaker.BrokenCircuitException(), out var status, out _, out var retryAfter));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
        Assert.Equal(DownstreamFailure.CircuitOpenRetryAfterSeconds, retryAfter);
    }

    [Fact]
    public void DeadlineExceeded_MapsTo504_AndConnectionFailure_MapsTo502()
    {
        Assert.True(DownstreamFailure.TryMap(new Polly.Timeout.TimeoutRejectedException(), out var timeoutStatus, out _, out _));
        Assert.Equal(StatusCodes.Status504GatewayTimeout, timeoutStatus);

        Assert.True(DownstreamFailure.TryMap(new HttpRequestException("refused"), out var connStatus, out _, out _));
        Assert.Equal(StatusCodes.Status502BadGateway, connStatus);
    }

    [Fact]
    public void BusinessExceptions_AreNotMapped()
    {
        Assert.False(DownstreamFailure.TryMap(new InvalidOperationException("business rule"), out _, out _, out _));
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("circuit")]
    [InlineData("timeout")]
    public async Task ReleaseReservation_IsBestEffort_WhenSchedulingIsUnavailable(string failure)
    {
        var client = new SchedulingClient(
            new HttpClient(new ThrowingHandler(failure)) { BaseAddress = new Uri("http://scheduling.test/") },
            NullLogger<SchedulingClient>.Instance);

        var released = await client.ReleaseReservationAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(released);
    }

    private sealed class ThrowingHandler(string failure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            failure switch
            {
                "circuit" => throw new Polly.CircuitBreaker.BrokenCircuitException(),
                "timeout" => throw new Polly.Timeout.TimeoutRejectedException(),
                _ => throw new HttpRequestException("Connection refused", null, HttpStatusCode.ServiceUnavailable)
            };
    }
}
