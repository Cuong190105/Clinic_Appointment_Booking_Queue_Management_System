using System.Net;
using System.Net.Http.Json;

namespace Clinic.MedicalRecords.Service.Services;

public sealed class QueueCompletionClient(IHttpClientFactory clients)
{
    public async Task<HttpStatusCode?> CompleteIfInProgressAsync(Guid appointmentId, string? authorization, CancellationToken ct)
    {
        var client = clients.CreateClient("Queue");
        using var lookup = new HttpRequestMessage(HttpMethod.Get, $"queue/appointment/{appointmentId}");
        ForwardAuthorization(lookup, authorization);
        using var lookupResponse = await client.SendAsync(lookup, ct);
        if (lookupResponse.StatusCode == HttpStatusCode.NotFound) return null;
        if (!lookupResponse.IsSuccessStatusCode) return lookupResponse.StatusCode;

        var ticket = await lookupResponse.Content.ReadFromJsonAsync<QueueTicketSnapshot>(cancellationToken: ct);
        if (ticket is null) return HttpStatusCode.BadGateway;
        if (!string.Equals(ticket.Status, "InProgress", StringComparison.OrdinalIgnoreCase)) return null;

        using var complete = new HttpRequestMessage(HttpMethod.Post, $"queue/{ticket.Id}/complete-exam");
        ForwardAuthorization(complete, authorization);
        using var completeResponse = await client.SendAsync(complete, ct);
        return completeResponse.IsSuccessStatusCode ? null : completeResponse.StatusCode;
    }

    private static void ForwardAuthorization(HttpRequestMessage request, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) request.Headers.TryAddWithoutValidation("Authorization", value);
    }

    private sealed record QueueTicketSnapshot(Guid Id, string Status);
}
