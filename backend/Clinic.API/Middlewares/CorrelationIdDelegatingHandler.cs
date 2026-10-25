using Clinic.Application.Interfaces;

namespace Clinic.API.Middlewares;

public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly ICorrelationContext _correlationContext;

    public CorrelationIdDelegatingHandler(ICorrelationContext correlationContext)
    {
        _correlationContext = correlationContext;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_correlationContext.CorrelationId is { } correlationId &&
            correlationId != Guid.Empty &&
            !request.Headers.Contains("X-Correlation-ID"))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
