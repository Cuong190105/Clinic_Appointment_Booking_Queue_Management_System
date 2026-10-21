namespace Clinic.Scheduling.Service.Services;

using Clinic.Scheduling.Service.Enums;
using Clinic.Scheduling.Service.Persistence;
using Microsoft.EntityFrameworkCore;

public class ReservationExpirationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReservationExpirationWorker> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(15);

    public ReservationExpirationWorker(IServiceProvider serviceProvider, ILogger<ReservationExpirationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReservationExpirationWorker is starting.");

        using var timer = new PeriodicTimer(_checkInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ExpireOldReservationsAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred while expiring old reservations.");
            }
        }

        _logger.LogInformation("ReservationExpirationWorker is stopping.");
    }

    public async Task<int> ExpireOldReservationsAsync(CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();

        var now = DateTimeOffset.UtcNow;
        var expiredCount = await dbContext.SlotReservations
            .Where(r => r.Status == ReservationStatus.Reserved && r.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, ReservationStatus.Expired), ct);

        if (expiredCount > 0)
        {
            _logger.LogInformation("Expired {Count} slot reservations at {Time}.", expiredCount, now);
        }

        return expiredCount;
    }
}

