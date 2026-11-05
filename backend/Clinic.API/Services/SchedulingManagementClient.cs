namespace Clinic.API.Services;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

public sealed class SchedulingManagementClient(IHttpClientFactory clients)
{
    private readonly HttpClient _client = clients.CreateClient("Scheduling");

    public async Task<IActionResult> ProxyAsync(
        HttpContext httpContext,
        string targetPath,
        HttpMethod? method = null,
        object? jsonBody = null,
        CancellationToken ct = default)
    {
        var httpMethod = method ?? new HttpMethod(httpContext.Request.Method);
        var path = targetPath;
        if (!path.Contains('?') && httpContext.Request.QueryString.HasValue)
        {
            path += httpContext.Request.QueryString.Value;
        }

        using var request = new HttpRequestMessage(httpMethod, path);

        if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            request.Headers.TryAddWithoutValidation("Authorization", (string?)authHeader);
        }

        if (httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var idempHeader))
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", (string?)idempHeader);
        }

        if (jsonBody != null)
        {
            var json = JsonSerializer.Serialize(jsonBody);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        else if (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put || httpMethod.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase))
        {
            httpContext.Request.EnableBuffering();
            if (httpContext.Request.Body.CanSeek)
            {
                httpContext.Request.Body.Position = 0;
            }
            using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync(ct);
            if (!string.IsNullOrWhiteSpace(body))
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }
        }

        try
        {
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
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new ObjectResult(Clinic.API.Models.ApiResponse.Fail(HttpStatusCode.GatewayTimeout, "Scheduling service request timed out."))
            {
                StatusCode = StatusCodes.Status504GatewayTimeout
            };
        }
        catch (HttpRequestException)
        {
            return new ObjectResult(Clinic.API.Models.ApiResponse.Fail(HttpStatusCode.BadGateway, "Staff & Scheduling Service is unavailable."))
            {
                StatusCode = StatusCodes.Status502BadGateway
            };
        }
    }

    public async Task<SchedulingManagementResponse> SendAsync(HttpContext context, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (context.Request.Headers.TryGetValue("Authorization", out var auth)) request.Headers.TryAddWithoutValidation("Authorization", auth.ToArray());
        if (body is not null)
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            request.Content = JsonContent.Create(body, options: options);
        }
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        return new SchedulingManagementResponse((int)response.StatusCode, response.Content.Headers.ContentType?.ToString() ?? "application/json", content);
    }
}

public sealed record SchedulingManagementResponse(int StatusCode, string ContentType, string Content)
{
    public Microsoft.AspNetCore.Mvc.IActionResult ToActionResult() => new Microsoft.AspNetCore.Mvc.ContentResult
    {
        StatusCode = StatusCode,
        ContentType = ContentType,
        Content = Content
    };

    public JsonDocument? ParseJson() => string.IsNullOrWhiteSpace(Content) ? null : JsonDocument.Parse(Content);
}
