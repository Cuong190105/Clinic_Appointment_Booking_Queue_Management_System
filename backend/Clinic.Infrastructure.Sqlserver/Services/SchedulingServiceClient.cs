namespace Clinic.Infrastructure.Sqlserver.Services;

using System.Net.Http.Json;
using System.Text.Json;
using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Domain.Common.Exceptions;
using Microsoft.Extensions.Logging;

public class SchedulingServiceClient : ISchedulingServiceClient
{
    private readonly HttpClient _client;
    private readonly ILogger<SchedulingServiceClient> _logger;

    public SchedulingServiceClient(IHttpClientFactory httpClientFactory, ILogger<SchedulingServiceClient> logger)
    {
        _client = httpClientFactory.CreateClient("Scheduling");
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
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new ConflictException("An appointment already exists for this time slot.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new NotFoundException($"Work schedule not found.");
        }

        response.EnsureSuccessStatusCode();

        var res = await response.Content.ReadFromJsonAsync<ScheduleReservationResult>(cancellationToken: ct);
        return res;
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
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = doc.RootElement;
        if (!root.TryGetProperty("result", out var resultEl))
            return null;

        return new ScheduleInfoResult(
            resultEl.GetProperty("id").GetGuid(),
            resultEl.GetProperty("doctorId").GetGuid(),
            resultEl.GetProperty("doctorName").GetString() ?? string.Empty,
            resultEl.TryGetProperty("specialtyName", out var specEl) ? specEl.GetString() : null,
            DateOnly.Parse(resultEl.GetProperty("date").GetString()!),
            TimeOnly.Parse(resultEl.GetProperty("shiftStart").GetString()!),
            TimeOnly.Parse(resultEl.GetProperty("shiftEnd").GetString()!),
            resultEl.GetProperty("recommendedCapacity").GetInt32(),
            resultEl.GetProperty("status").GetString() ?? string.Empty
        );
    }
    public async Task<DoctorDashboardStatsResult?> GetDoctorDashboardStatsAsync(CancellationToken ct = default)
    {
        var response = await _client.GetAsync("doctors/dashboard-stats", ct);
        if (!response.IsSuccessStatusCode)
            return null;

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = doc.RootElement;
        var resultEl = root.TryGetProperty("result", out var r) ? r : root;
        return new DoctorDashboardStatsResult(
            resultEl.GetProperty("totalDoctors").GetInt32(),
            resultEl.GetProperty("totalDoctorsChange").GetInt32(),
            resultEl.GetProperty("activeDoctors").GetInt32(),
            resultEl.GetProperty("activeDoctorsChange").GetInt32()
        );
    }
}
