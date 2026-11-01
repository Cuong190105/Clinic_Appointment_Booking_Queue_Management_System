using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Clinic.API.Services;

namespace Clinic.API.Middlewares;

/// <summary>Routes legacy Staff/Scheduling HTTP contracts to Scheduling while Clinic keeps the external auth policy.</summary>
public sealed class SchedulingProxyMiddleware(RequestDelegate next, IHttpClientFactory clients, IdentityAccountClient identityAccounts, IAuthorizationService authorizationService, ILogger<SchedulingProxyMiddleware> logger)
{
    private readonly HttpClient _client = clients.CreateClient("Scheduling");

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;
        var normalized = path.TrimEnd('/');
        var isQueueRoute = normalized.Contains("/queue", StringComparison.OrdinalIgnoreCase);
        var isIdentityProvisioningRoute = method == HttpMethods.Post &&
            (normalized.Equals("/employees", StringComparison.OrdinalIgnoreCase) ||
             normalized.Equals("/employees/from-user", StringComparison.OrdinalIgnoreCase) ||
             normalized.Equals("/doctors", StringComparison.OrdinalIgnoreCase) ||
             normalized.Equals("/doctors/from-user", StringComparison.OrdinalIgnoreCase));
        var isIdentityProfileUpdateRoute = method == HttpMethods.Put &&
            (normalized.Equals("/employees/me", StringComparison.OrdinalIgnoreCase) ||
             normalized.Equals("/doctors/me", StringComparison.OrdinalIgnoreCase) ||
             (normalized.StartsWith("/employees/", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(normalized["/employees/".Length..], out _)) ||
             (normalized.StartsWith("/doctors/", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(normalized["/doctors/".Length..], out _)));
        var isStaffSchedulingRoute = normalized.StartsWith("/employees", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("/doctors", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("/specialties", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("/shifts", StringComparison.OrdinalIgnoreCase);

        if (!isStaffSchedulingRoute || isQueueRoute || isIdentityProvisioningRoute || isIdentityProfileUpdateRoute)
        {
            await next(context);
            return;
        }

        var requiredPermission = GetRequiredPermission(normalized, method);
        if (requiredPermission is not null)
        {
            var authorizationResult = await authorizationService.AuthorizeAsync(
                context.User,
                resource: null,
                policyName: $"Permission:{requiredPermission}");
            if (!authorizationResult.Succeeded)
            {
                if (context.User.Identity?.IsAuthenticated == true)
                    await context.ForbidAsync();
                else
                    await context.ChallengeAsync();
                return;
            }
        }

        using var request = new HttpRequestMessage(new HttpMethod(method), path + context.Request.QueryString);
        if (context.Request.Headers.TryGetValue("Authorization", out var authorization))
            request.Headers.TryAddWithoutValidation("Authorization", authorization.ToArray());
        if (context.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey))
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey.ToArray());

        if (context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            request.Content = new StreamContent(context.Request.Body);
            if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
                request.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
        }
        try
        {
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            context.Response.StatusCode = (int)response.StatusCode;
            foreach (var header in response.Headers)
                context.Response.Headers[header.Key] = header.Value.ToArray();
            foreach (var header in response.Content.Headers)
                context.Response.Headers[header.Key] = header.Value.ToArray();
            context.Response.Headers.Remove("transfer-encoding");
            if (response.IsSuccessStatusCode && method == HttpMethods.Get && normalized.StartsWith("/employees", StringComparison.OrdinalIgnoreCase))
            {
                var payload = await response.Content.ReadAsStringAsync(context.RequestAborted);
                var enriched = await EnrichEmployeeRolesAsync(normalized, payload, context.RequestAborted);
                context.Response.ContentLength = null;
                await context.Response.WriteAsync(enriched, context.RequestAborted);
            }
            else
                await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Scheduling proxy failed for {Method} {Path}", method, path);
            context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
            await context.Response.WriteAsJsonAsync(new { message = "Scheduling Service is unavailable." }, context.RequestAborted);
        }
    }

    private static string? GetRequiredPermission(string path, string method)
    {
        if (path.Equals("/specialties", StringComparison.OrdinalIgnoreCase) && method == HttpMethods.Post)
            return "specialty.manage";

        if (path.StartsWith("/specialties/", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = path["/specialties/".Length..];
            if (method == HttpMethods.Put && Guid.TryParse(suffix, out _))
                return "specialty.manage";
            if (method == HttpMethods.Patch && suffix.EndsWith("/status", StringComparison.OrdinalIgnoreCase) &&
                Guid.TryParse(suffix[..^"/status".Length], out _))
                return "specialty.manage";
        }

        return null;
    }

    private async Task<string> EnrichEmployeeRolesAsync(string path, string payload, CancellationToken ct)
    {
        try
        {
            var root = JsonNode.Parse(payload);
            var employees = new List<JsonObject>();
            if (root is JsonObject obj && obj["items"] is JsonArray items) employees.AddRange(items.OfType<JsonObject>());
            else if (root is JsonObject single) employees.Add(single);
            foreach (var employee in employees)
            {
                if (employee["userId"] is JsonValue userValue && Guid.TryParse(userValue.ToString(), out var userId))
                {
                    var identity = await identityAccounts.GetRolesAsync(userId, ct);
                    if (identity is not null) employee["roles"] = JsonSerializer.SerializeToNode(identity.Value.Roles);
                }
            }
            return root?.ToJsonString() ?? payload;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not enrich Scheduling employee response with Identity roles for {Path}", path);
            return payload;
        }
    }
}
