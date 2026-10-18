using System.Diagnostics;

namespace Clinic.API.Middlewares;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, Clinic.Application.Interfaces.ICorrelationContext correlationContext)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = GetOrCreateCorrelationId(context);
        correlationContext.Set(correlationId);
        context.Response.Headers["X-Correlation-ID"] = correlationId.ToString();
        Activity.Current?.SetTag("clinic.correlation_id", correlationId);
        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = Activity.Current?.TraceId.ToString() ?? string.Empty
        });

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            _logger.LogInformation(
                "{Method} {Path}{QueryString} responded {StatusCode} in {ElapsedMs} ms",
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private static Guid GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var values) &&
            Guid.TryParse(values.FirstOrDefault(), out var correlationId) &&
            correlationId != Guid.Empty)
        {
            return correlationId;
        }

        return Guid.NewGuid();
    }
}
