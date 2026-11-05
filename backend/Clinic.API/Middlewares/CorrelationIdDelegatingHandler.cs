using Microsoft.AspNetCore.Http;

namespace Clinic.API.Middlewares;

public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly ICorrelationContext _correlationContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdDelegatingHandler(
        ICorrelationContext correlationContext,
        IHttpContextAccessor httpContextAccessor)
    {
        _correlationContext = correlationContext;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains("X-Correlation-ID"))
        {
            var correlationId = _correlationContext.CorrelationId;
            if (correlationId is null || correlationId == Guid.Empty)
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null)
                {
                    if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var reqVal) &&
                        Guid.TryParse(reqVal.FirstOrDefault(), out var parsed) && parsed != Guid.Empty)
                    {
                        correlationId = parsed;
                    }
                    else if (httpContext.Response.Headers.TryGetValue("X-Correlation-ID", out var resVal) &&
                        Guid.TryParse(resVal.FirstOrDefault(), out var resParsed) && resParsed != Guid.Empty)
                    {
                        correlationId = resParsed;
                    }
                }
            }

            if (correlationId is { } validId && validId != Guid.Empty)
            {
                request.Headers.TryAddWithoutValidation("X-Correlation-ID", validId.ToString());
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
