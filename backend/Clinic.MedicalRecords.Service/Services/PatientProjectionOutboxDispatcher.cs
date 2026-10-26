using System.Net.Http.Json;
using Clinic.Contracts.Events;
using Clinic.MedicalRecords.Service.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.MedicalRecords.Service.Services;

public sealed class PatientProjectionOutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IHttpClientFactory _clients;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PatientProjectionOutboxDispatcher> _logger;

    public PatientProjectionOutboxDispatcher(
        IServiceScopeFactory scopes,
        IHttpClientFactory clients,
        IConfiguration configuration,
        ILogger<PatientProjectionOutboxDispatcher> logger)
    {
        _scopes = scopes;
        _clients = clients;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Patient projection outbox dispatcher failed; it will retry.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MedicalRecordsDbContext>();
        var messages = await db.PatientProjectionOutbox
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(25)
            .ToListAsync(ct);
        if (messages.Count == 0) return;

        var token = _configuration["Identity:ServiceToken"];
        var client = _clients.CreateClient("AppointmentProjection");
        foreach (var message in messages)
        {
            var patient = await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == message.PatientId, ct);
            if (patient is null)
            {
                message.ProcessedAt = DateTimeOffset.UtcNow;
                continue;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "internal/events/user-profile-updated")
                {
                    Content = JsonContent.Create(new UserProfileUpdated(
                        patient.PersonId ?? patient.Id,
                        patient.FullName,
                        patient.PhoneNumber,
                        patient.Email,
                        patient.Gender,
                        patient.DateOfBirth,
                        patient.Address,
                        patient.Id,
                        patient.IdentityUserId))
                };
                if (!string.IsNullOrWhiteSpace(token))
                    request.Headers.TryAddWithoutValidation("X-Clinic-Service-Token", token);

                using var response = await client.SendAsync(request, ct);
                message.AttemptCount++;
                message.LastAttemptAt = DateTimeOffset.UtcNow;
                if (response.IsSuccessStatusCode)
                {
                    message.ProcessedAt = DateTimeOffset.UtcNow;
                    message.LastError = null;
                    _logger.LogInformation("Appointment patient projection synchronized for PatientId={PatientId}", message.PatientId);
                }
                else
                {
                    message.LastError = $"Appointment returned HTTP {(int)response.StatusCode}.";
                    _logger.LogWarning("Patient projection sync deferred for PatientId={PatientId}, StatusCode={StatusCode}",
                        message.PatientId, (int)response.StatusCode);
                }
            }
            catch (HttpRequestException ex)
            {
                message.AttemptCount++;
                message.LastAttemptAt = DateTimeOffset.UtcNow;
                message.LastError = "Appointment service is unreachable.";
                _logger.LogWarning("Patient projection sync deferred for PatientId={PatientId}: {FailureType}",
                    message.PatientId, ex.GetType().Name);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                message.AttemptCount++;
                message.LastAttemptAt = DateTimeOffset.UtcNow;
                message.LastError = "Appointment service request timed out.";
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
