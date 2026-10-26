using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Services;

public sealed class NotificationServiceClient
{
    private readonly HttpClient _client;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationServiceClient> _logger;

    public NotificationServiceClient(
        IHttpClientFactory factory,
        IConfiguration config,
        ILogger<NotificationServiceClient> logger)
    {
        _client = factory.CreateClient("Notification");
        _config = config;
        _logger = logger;
    }

    public async Task<IActionResult> ProxyAsync(
        HttpContext httpContext,
        string targetPath,
        HttpMethod? method = null,
        object? jsonBody = null,
        CancellationToken ct = default)
    {
        var httpMethod = method ?? new HttpMethod(httpContext.Request.Method);

        // Append query string if targetPath does not already include one
        var uri = targetPath;
        if (!uri.Contains('?') && httpContext.Request.QueryString.HasValue)
        {
            uri += httpContext.Request.QueryString.Value;
        }

        using var request = new HttpRequestMessage(httpMethod, uri);

        // Forward Authorization header
        if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            request.Headers.TryAddWithoutValidation("Authorization", (string?)authHeader);
        }

        // Forward Correlation-Id
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var corrHeader))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", (string?)corrHeader);
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

    public async Task<bool> SendPasswordResetNotificationAsync(
        Guid recipientId,
        string? email,
        string? phone,
        string resetToken,
        CancellationToken ct = default)
    {
        try
        {
            var serviceToken = _config["Identity:ServiceToken"] ?? "integration-test-service-token";

            using var request = new HttpRequestMessage(HttpMethod.Post, "internal/notifications/password-reset")
            {
                Content = JsonContent.Create(new
                {
                    recipientId,
                    email,
                    phoneNumber = phone,
                    resetToken
                })
            };

            request.Headers.Add("X-Clinic-Service-Token", serviceToken);

            using var response = await _client.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Notification Service password-reset endpoint for RecipientId: {RecipientId}", recipientId);
            return false;
        }
    }
}
