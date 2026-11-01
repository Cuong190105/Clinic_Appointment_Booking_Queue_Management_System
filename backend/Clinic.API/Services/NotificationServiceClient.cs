using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
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

    public async Task<IActionResult> ProxyOperatorAsync(
        HttpContext httpContext,
        string targetPath,
        HttpMethod method,
        CancellationToken ct = default)
    {
        var serviceToken = _config["Identity:ServiceToken"];
        var operatorValue = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(serviceToken))
            return new StatusCodeResult((int)HttpStatusCode.ServiceUnavailable);
        if (!Guid.TryParse(operatorValue, out var operatorId))
            return new ForbidResult();

        var uri = targetPath;
        if (!uri.Contains('?') && httpContext.Request.QueryString.HasValue)
            uri += httpContext.Request.QueryString.Value;

        using var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("X-Clinic-Service-Token", serviceToken);
        request.Headers.TryAddWithoutValidation("X-Clinic-Operator-Id", operatorId.ToString("D"));
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId))
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", (string?)correlationId);

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return new NoContentResult();

        return new ContentResult
        {
            Content = await response.Content.ReadAsStringAsync(ct),
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            StatusCode = (int)response.StatusCode
        };
    }

    public async Task ProxySignalRAsync(
        HttpContext httpContext,
        string? subPath,
        CancellationToken ct = default)
    {
        var relativePath = string.IsNullOrWhiteSpace(subPath)
            ? "hubs/notifications"
            : $"hubs/notifications/{subPath.TrimStart('/')}";

        if (httpContext.Request.QueryString.HasValue)
        {
            relativePath += httpContext.Request.QueryString.Value;
        }

        if (httpContext.WebSockets.IsWebSocketRequest)
        {
            await ProxyWebSocketUpgradeAsync(httpContext, relativePath, ct);
            return;
        }

        var httpMethod = new HttpMethod(httpContext.Request.Method);
        using var request = new HttpRequestMessage(httpMethod, relativePath);

        if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) &&
            !string.IsNullOrWhiteSpace(authHeader))
        {
            request.Headers.TryAddWithoutValidation("Authorization", (string?)authHeader);
        }
        else if (httpContext.Request.Query.TryGetValue("access_token", out var queryToken) &&
                 !string.IsNullOrWhiteSpace(queryToken))
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {queryToken}");
        }

        if (httpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var corrHeader))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", (string?)corrHeader);
        }

        if (httpContext.Request.ContentLength > 0 &&
            (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put))
        {
            using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync(ct);
            request.Content = new StringContent(body, Encoding.UTF8, httpContext.Request.ContentType ?? "application/json");
        }

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        httpContext.Response.StatusCode = (int)response.StatusCode;
        if (response.Content.Headers.ContentType != null)
        {
            httpContext.Response.ContentType = response.Content.Headers.ContentType.ToString();
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length > 0)
        {
            await httpContext.Response.Body.WriteAsync(bytes, ct);
        }
    }

    private async Task ProxyWebSocketUpgradeAsync(
        HttpContext httpContext,
        string relativePath,
        CancellationToken ct)
    {
        var baseAddress = _client.BaseAddress ?? new Uri("http://localhost:5006/");
        var fullHttpUri = new Uri(baseAddress, relativePath);
        var wsBuilder = new UriBuilder(fullHttpUri)
        {
            Scheme = fullHttpUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws"
        };

        using var upstreamWs = new ClientWebSocket();
        foreach (var protocol in httpContext.WebSockets.WebSocketRequestedProtocols)
        {
            upstreamWs.Options.AddSubProtocol(protocol);
        }

        if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) &&
            !string.IsNullOrWhiteSpace(authHeader))
        {
            upstreamWs.Options.SetRequestHeader("Authorization", authHeader.ToString());
        }
        else if (httpContext.Request.Query.TryGetValue("access_token", out var queryToken) &&
                 !string.IsNullOrWhiteSpace(queryToken))
        {
            upstreamWs.Options.SetRequestHeader("Authorization", $"Bearer {queryToken}");
        }

        if (httpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var corrHeader) &&
            !string.IsNullOrWhiteSpace(corrHeader))
        {
            upstreamWs.Options.SetRequestHeader("X-Correlation-Id", corrHeader.ToString());
        }

        try
        {
            await upstreamWs.ConnectAsync(wsBuilder.Uri, ct);
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "Upstream SignalR WebSocket connection failed.");
            httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        using var downstreamWs = await httpContext.WebSockets.AcceptWebSocketAsync(upstreamWs.SubProtocol);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, httpContext.RequestAborted);
        var pumpDownToUp = PumpWebSocketAsync(downstreamWs, upstreamWs, linkedCts.Token);
        var pumpUpToDown = PumpWebSocketAsync(upstreamWs, downstreamWs, linkedCts.Token);

        await Task.WhenAny(pumpDownToUp, pumpUpToDown);
        linkedCts.Cancel();
    }

    private static async Task PumpWebSocketAsync(WebSocket source, WebSocket destination, CancellationToken ct)
    {
        var buffer = new byte[8 * 1024];
        try
        {
            while (source.State == WebSocketState.Open && destination.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await source.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (destination.State == WebSocketState.Open || destination.State == WebSocketState.CloseReceived)
                    {
                        await destination.CloseOutputAsync(
                            result.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                            result.CloseStatusDescription,
                            CancellationToken.None);
                    }
                    break;
                }

                await destination.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal teardown
        }
        catch (WebSocketException)
        {
            // Peer closed abruptly
        }
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
            var serviceToken = _config["Identity:ServiceToken"];
            if (string.IsNullOrWhiteSpace(serviceToken))
            {
                _logger.LogError("Identity:ServiceToken is not configured; cannot call Notification Service password-reset endpoint.");
                return false;
            }

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
