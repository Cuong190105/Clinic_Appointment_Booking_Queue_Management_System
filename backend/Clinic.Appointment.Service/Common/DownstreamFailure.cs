namespace Clinic.Appointment.Service.Common;

/// <summary>
/// Maps failures of the synchronous Scheduling dependency to explicit QoS status codes
/// (SLI_SLO_MATRIX §5.2 / §5.4 degraded modes).
/// </summary>
public static class DownstreamFailure
{
    public const int CircuitOpenRetryAfterSeconds = 15;

    /// <summary>
    /// Marks a 5xx that was caused by a dependency (Scheduling), not by Appointment itself, so the
    /// Gateway's circuit breaker for Appointment does not open and cut off unrelated Appointment routes.
    /// </summary>
    public const string DependencyFailureHeader = "X-Clinic-Dependency-Failure";

    public static bool TryMap(Exception exception, out int statusCode, out string message, out int? retryAfterSeconds)
    {
        retryAfterSeconds = null;
        switch (exception)
        {
            case Polly.CircuitBreaker.BrokenCircuitException:
                statusCode = StatusCodes.Status503ServiceUnavailable;
                message = "Scheduling service is temporarily unavailable (circuit open). Please retry later.";
                retryAfterSeconds = CircuitOpenRetryAfterSeconds;
                return true;
            case Polly.Timeout.TimeoutRejectedException:
                statusCode = StatusCodes.Status504GatewayTimeout;
                message = "Scheduling service did not respond within the SLA deadline.";
                return true;
            case HttpRequestException:
                statusCode = StatusCodes.Status502BadGateway;
                message = "Scheduling service connection failed.";
                return true;
            default:
                statusCode = 0;
                message = string.Empty;
                return false;
        }
    }
}
