using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Clinic.API.Services;

public sealed class SchedulingManagementClient(IHttpClientFactory clients)
{
    public async Task<SchedulingManagementResponse> SendAsync(HttpContext context, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var client = clients.CreateClient("Scheduling");
        using var request = new HttpRequestMessage(method, path);
        if (context.Request.Headers.TryGetValue("Authorization", out var auth)) request.Headers.TryAddWithoutValidation("Authorization", auth.ToArray());
        if (body is not null)
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            request.Content = JsonContent.Create(body, options: options);
        }
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
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
