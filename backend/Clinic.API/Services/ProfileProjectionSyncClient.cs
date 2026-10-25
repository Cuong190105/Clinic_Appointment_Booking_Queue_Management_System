using System.Net.Http.Json;
using Clinic.Contracts.Events;

namespace Clinic.API.Services;

/// <summary>Updates the Scheduling profile projection while Identity remains the profile source of truth.</summary>
public sealed class ProfileProjectionSyncClient(IHttpClientFactory clients, IConfiguration configuration)
{
    public async Task<bool> SyncAsync(UserProfileUpdated evt, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/events/user-profile-updated")
        {
            Content = JsonContent.Create(evt)
        };
        var serviceToken = configuration["Identity:ServiceToken"];
        if (!string.IsNullOrWhiteSpace(serviceToken))
            request.Headers.TryAddWithoutValidation("X-Clinic-Service-Token", serviceToken);
        using var response = await clients.CreateClient("Scheduling").SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }
}
