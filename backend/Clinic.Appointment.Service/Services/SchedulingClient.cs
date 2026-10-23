namespace Clinic.Appointment.Service.Services;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

public class SchedulingClient : ISchedulingClient
{
    private readonly HttpClient _client;
    private readonly ILogger<SchedulingClient> _logger;

    public SchedulingClient(HttpClient client, ILogger<SchedulingClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<ScheduleReservationResult?> ReserveSlotAsync(Guid scheduleId, TimeOnly timeSlot, Guid patientId, string idempotencyKey, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"schedules/{scheduleId}/reservations")
        {
            Content = JsonContent.Create(new { timeSlot, patientId })
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        var response = await _client.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return null;
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ScheduleReservationResult>(cancellationToken: ct);
    }

    public async Task<bool> ConfirmReservationAsync(Guid scheduleId, Guid reservationId, CancellationToken ct = default)
    {
        var response = await _client.PostAsync($"schedules/{scheduleId}/reservations/{reservationId}/confirm", null, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ReleaseReservationAsync(Guid scheduleId, Guid reservationId, CancellationToken ct = default)
    {
        var response = await _client.PostAsync($"schedules/{scheduleId}/reservations/{reservationId}/release", null, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<ScheduleInfoResult?> GetScheduleAsync(Guid scheduleId, CancellationToken ct = default)
    {
        var response = await _client.GetAsync($"schedules/{scheduleId}", ct);
        if (!response.IsSuccessStatusCode)
            return null;

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = doc.RootElement;
        var target = root.TryGetProperty("result", out var resultEl) ? resultEl : root;

        return new ScheduleInfoResult(
            target.GetProperty("id").GetGuid(),
            target.GetProperty("doctorId").GetGuid(),
            target.GetProperty("doctorName").GetString() ?? string.Empty,
            target.TryGetProperty("specialtyName", out var specEl) ? specEl.GetString() : null,
            DateOnly.Parse(target.GetProperty("date").GetString()!),
            TimeOnly.Parse(target.GetProperty("shiftStart").GetString()!),
            TimeOnly.Parse(target.GetProperty("shiftEnd").GetString()!),
            target.GetProperty("recommendedCapacity").GetInt32(),
            target.GetProperty("status").GetString() ?? string.Empty
        );
    }
}

