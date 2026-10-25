using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Services;

public sealed class AppointmentServiceClient
{
    private readonly HttpClient _client;
    private readonly string? _serviceToken;

    public AppointmentServiceClient(IHttpClientFactory factory, IConfiguration configuration)
    {
        _client = factory.CreateClient("Appointment");
        _serviceToken = configuration["Identity:ServiceToken"];
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

        // Forward request body
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
        var json = JsonSerializer.Serialize(jsonBody);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(HttpMethod.Post, targetPath) { Content = content };
                if (!string.IsNullOrWhiteSpace(_serviceToken))
                    request.Headers.TryAddWithoutValidation("X-Clinic-Service-Token", _serviceToken);
                using var response = await _client.SendAsync(request, ct);
                if (response.IsSuccessStatusCode) return true;
                if ((int)response.StatusCode < 500 && response.StatusCode != HttpStatusCode.RequestTimeout && response.StatusCode != HttpStatusCode.TooManyRequests)
                    return false;
            }
            catch (HttpRequestException) when (!ct.IsCancellationRequested)
            {
                // Retry transient service/network failures; the receiving endpoint is idempotent by appointment ID.
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // HttpClient timeout; retry with the same stable appointment ID.
            }

            if (attempt < 3)
                await Task.Delay(TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt)), ct);
        }

        return false;
    }

    public async Task<JsonDocument?> GetJsonAsync(HttpContext httpContext, string targetPath, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, targetPath);
            if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                request.Headers.TryAddWithoutValidation("Authorization", (string?)authHeader);
            }
            using var response = await _client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;
            var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch
        {
            return null;
        }
    }

    public async Task<T?> GetAsync<T>(HttpContext httpContext, string targetPath, CancellationToken ct = default)
    {
        using var document = await GetJsonAsync(httpContext, targetPath, ct);
        if (document is null) return default;
        return JsonSerializer.Deserialize<T>(document.RootElement.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    public async Task<ServiceReadResult<T>> GetResultAsync<T>(HttpContext httpContext, string targetPath, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, targetPath);
            if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
                request.Headers.TryAddWithoutValidation("Authorization", authHeader.ToArray());
            using var response = await _client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return new ServiceReadResult<T>(response.StatusCode, default);
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            return new ServiceReadResult<T>(response.StatusCode, value);
        }
        catch (HttpRequestException) when (!ct.IsCancellationRequested)
        {
            return new ServiceReadResult<T>(HttpStatusCode.ServiceUnavailable, default);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ServiceReadResult<T>(HttpStatusCode.ServiceUnavailable, default);
        }
    }
}

public sealed record ServiceReadResult<T>(HttpStatusCode StatusCode, T? Value);

