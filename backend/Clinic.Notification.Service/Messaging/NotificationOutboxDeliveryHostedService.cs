using Clinic.Notification.Service.Entities;
using Clinic.Notification.Service.Persistence;
using Clinic.Notification.Service.Services.Delivery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clinic.Notification.Service.Messaging;

public sealed class NotificationOutboxDeliveryHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationOutboxDeliveryHostedService> _logger;

    public NotificationOutboxDeliveryHostedService(
        IServiceProvider serviceProvider,
        ILogger<NotificationOutboxDeliveryHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationOutboxDeliveryHostedService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingDeliveriesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred during outbox delivery cycle.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("NotificationOutboxDeliveryHostedService stopped.");
    }

    private async Task ProcessPendingDeliveriesAsync(CancellationToken ct)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDeliveryDispatcher>();

        var now = DateTimeOffset.UtcNow;
        var pendingIds = await db.DeliveryOutboxMessages
            .Where(m => m.Status == DeliveryStatus.Pending &&
                        (m.ScheduledFor == null || m.ScheduledFor <= now) &&
                        (m.NextAttemptAt == null || m.NextAttemptAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(20)
            .Select(m => m.Id)
            .ToListAsync(ct);

        foreach (var deliveryId in pendingIds)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await dispatcher.ProcessDeliveryAsync(deliveryId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled failure processing delivery outbox message {DeliveryId}", deliveryId);
            }
        }
    }
}

