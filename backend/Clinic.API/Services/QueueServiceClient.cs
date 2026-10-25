using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Clinic.Contracts.Analytics;

namespace Clinic.API.Services;

public sealed class QueueServiceClient
{
    private readonly HttpClient _client;

    public QueueServiceClient(IHttpClientFactory factory)
    {
        _client = factory.CreateClient("Queue");
    }

    public async Task<IActionResult> ProxyAsync(
        HttpContext httpContext,
        string targetPath,
        HttpMethod? method = null,
        object? jsonBody = null,
        CancellationToken ct = default)
    {
        var httpMethod = method ?? new HttpMethod(httpContext.Request.Method);
        using var request = new HttpRequestMessage(httpMethod, targetPath);

        // Forward Authorization header
        if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            request.Headers.TryAddWithoutValidation("Authorization", (string?)authHeader);
        }

        // Forward Idempotency-Key header
        if (httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var idempHeader))
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", (string?)idempHeader);
        }

        // Forward request body if any
        if (jsonBody != null)
        {
            var json = JsonSerializer.Serialize(jsonBody);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        else if (httpContext.Request.ContentLength > 0 &&
                 (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put || httpMethod.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase)))
        {
            using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync(ct);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new NoContentResult();
        }

        var responseContent = await response.Content.ReadAsStringAsync(ct);
        return new ContentResult
        {
            Content = responseContent,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            StatusCode = (int)response.StatusCode
        };
    }

    public async Task<bool> PostInternalAsync(string targetPath, object jsonBody, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(jsonBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _client.PostAsync(targetPath, content, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<QueueDashboardMetrics?> GetDashboardMetricsAsync(HttpContext context, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "admin/dashboard-metrics");
            if (context.Request.Headers.TryGetValue("Authorization", out var auth))
                request.Headers.TryAddWithoutValidation("Authorization", auth.ToArray());
            using var response = await _client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<QueueDashboardMetrics>(cancellationToken: ct);
        }
        catch (HttpRequestException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    public async Task<ServiceReadResult<QueueTicketLookup>> GetTicketByAppointmentAsync(HttpContext context, Guid appointmentId, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"queue/appointment/{appointmentId}");
            if (context.Request.Headers.TryGetValue("Authorization", out var auth))
                request.Headers.TryAddWithoutValidation("Authorization", auth.ToArray());
            using var response = await _client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return new ServiceReadResult<QueueTicketLookup>(response.StatusCode, null);
            var ticket = await response.Content.ReadFromJsonAsync<QueueTicketLookup>(cancellationToken: ct);
            return new ServiceReadResult<QueueTicketLookup>(response.StatusCode, ticket);
        }
        catch (HttpRequestException) when (!ct.IsCancellationRequested)
        {
            return new ServiceReadResult<QueueTicketLookup>(HttpStatusCode.ServiceUnavailable, null);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ServiceReadResult<QueueTicketLookup>(HttpStatusCode.ServiceUnavailable, null);
        }
    }
}

public sealed record QueueTicketLookup(Guid Id, string Status, DateTime? ExamStartedAt);

