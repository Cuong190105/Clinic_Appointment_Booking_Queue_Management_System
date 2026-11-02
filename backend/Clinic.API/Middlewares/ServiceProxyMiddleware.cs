using System.Net;

namespace Clinic.API.Middlewares;

/// <summary>Routes extracted bounded contexts while preserving the public Gateway contract.</summary>
public sealed class ServiceProxyMiddleware(RequestDelegate next, IHttpClientFactory clients, ILogger<ServiceProxyMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var medical = path.StartsWith("/medical-reports", StringComparison.OrdinalIgnoreCase) ||
                      path.Equals("/me/medical-history", StringComparison.OrdinalIgnoreCase) ||
                      path.Equals("/admin/medical-history/rebuild", StringComparison.OrdinalIgnoreCase) ||
                      (path.StartsWith("/patients/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/medical-history", StringComparison.OrdinalIgnoreCase));
        var notification = path.StartsWith("/me/notifications", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/admin/deliveries", StringComparison.OrdinalIgnoreCase);
        if (!medical && !notification) { await next(context); return; }
        var client = clients.CreateClient(medical ? "MedicalRecords" : "Notification");
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), path + context.Request.QueryString);
        foreach (var name in new[] { "Authorization", "Idempotency-Key", "X-Correlation-ID" })
            if (context.Request.Headers.TryGetValue(name, out var value)) request.Headers.TryAddWithoutValidation(name, value.ToArray());
        if (context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            request.Content = new StreamContent(context.Request.Body);
            if (!string.IsNullOrWhiteSpace(context.Request.ContentType)) request.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
        }
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            context.Response.StatusCode = (int)response.StatusCode;
            foreach (var header in response.Headers) context.Response.Headers[header.Key] = header.Value.ToArray();
            foreach (var header in response.Content.Headers) context.Response.Headers[header.Key] = header.Value.ToArray();
            context.Response.Headers.Remove("transfer-encoding"); await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Service proxy failed for {Method} {Path}", context.Request.Method, path);
            context.Response.StatusCode = (int)HttpStatusCode.BadGateway; await context.Response.WriteAsJsonAsync(new { message = "Upstream service is unavailable." }, context.RequestAborted);
        }
    }
}
