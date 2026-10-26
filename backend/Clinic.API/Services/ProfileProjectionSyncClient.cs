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
        if (!response.IsSuccessStatusCode) return false;

        using var appointmentRequest = new HttpRequestMessage(HttpMethod.Post, "internal/events/user-profile-updated")
        {
            Content = JsonContent.Create(evt)
        };
        if (!string.IsNullOrWhiteSpace(serviceToken))
            appointmentRequest.Headers.TryAddWithoutValidation("X-Clinic-Service-Token", serviceToken);
        using var appointmentResponse = await clients.CreateClient("Appointment").SendAsync(appointmentRequest, ct);
        if (!appointmentResponse.IsSuccessStatusCode) return false;

        if (evt.PatientId.HasValue && evt.IdentityUserId.HasValue)
        {
            using var medicalRecordsRequest = new HttpRequestMessage(HttpMethod.Post, "internal/events/user-profile-updated")
            {
                Content = JsonContent.Create(evt)
            };
            if (!string.IsNullOrWhiteSpace(serviceToken))
                medicalRecordsRequest.Headers.TryAddWithoutValidation("X-Clinic-Service-Token", serviceToken);
            using var medicalRecordsResponse = await clients.CreateClient("MedicalRecords").SendAsync(medicalRecordsRequest, ct);
            if (!medicalRecordsResponse.IsSuccessStatusCode) return false;
        }

        return true;
    }
}
