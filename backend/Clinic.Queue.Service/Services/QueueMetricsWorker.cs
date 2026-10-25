using Clinic.Queue.Service.Common;
using Clinic.Queue.Service.Enums;
using Clinic.Queue.Service.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Queue.Service.Services;

public sealed class QueueMetricsWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QueueMetricsWorker> _logger;
    private readonly IConfiguration _configuration;

    public QueueMetricsWorker(IServiceScopeFactory scopeFactory, ILogger<QueueMetricsWorker> logger, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_configuration.GetValue<bool>("SkipBackgroundWorkers")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<QueueDbContext>();
                var waiting = await db.QueueTickets.LongCountAsync(t => !t.IsDeleted && t.Status == QueueStatus.Waiting, stoppingToken);
                var pendingQuery = db.OutboxMessages.Where(m => m.Status == OutboxMessageStatus.Pending ||
                    m.Status == OutboxMessageStatus.Failed || m.Status == OutboxMessageStatus.Publishing ||
                    m.Status == OutboxMessageStatus.DeadLetterPending || m.Status == OutboxMessageStatus.DeadLetterPublishing);
                var pending = await pendingQuery.LongCountAsync(stoppingToken);
                var oldest = pending == 0 ? (long?)0 : (long)Math.Max(0, (DateTimeOffset.UtcNow - (await pendingQuery.MinAsync(m => m.CreatedAt, stoppingToken))).TotalSeconds);
                QueueTelemetry.UpdateSnapshot(waiting, pending, oldest ?? 0);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to refresh Queue metrics snapshot.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
