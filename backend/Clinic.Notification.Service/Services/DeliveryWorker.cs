using Clinic.Notification.Service.Entities;
using Clinic.Notification.Service.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Notification.Service.Services;

public sealed class DeliveryWorker(IServiceScopeFactory scopes, ILogger<DeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await DeliverBatchAsync(ct); await Task.Delay(TimeSpan.FromSeconds(2), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Notification delivery cycle failed"); await Task.Delay(TimeSpan.FromSeconds(5), ct); }
        }
    }

    private async Task DeliverBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>(); var now = DateTimeOffset.UtcNow;
        var ids = await db.Deliveries.Where(x =>
                ((x.Status == DeliveryStatus.Pending || x.Status == DeliveryStatus.RetryScheduled) && x.NextAttemptAt <= now) ||
                (x.Status == DeliveryStatus.Processing && x.ProcessingLeaseUntil <= now))
            .OrderBy(x => x.NextAttemptAt).Select(x => x.Id).Take(20).ToListAsync(ct);
        foreach (var id in ids)
            await scope.ServiceProvider.GetRequiredService<DeliveryProcessor>().ProcessAsync(id, ct);
    }
}

public sealed class DeliveryProcessor(NotificationDbContext db, IEnumerable<IDeliveryProvider> providers)
{
    public async Task<bool> ProcessAsync(Guid id, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var leaseUntil = now.AddSeconds(45);
        bool claimed;
        if (db.Database.IsRelational())
        {
            claimed = await db.Deliveries.Where(x => x.Id == id &&
                    (((x.Status == DeliveryStatus.Pending || x.Status == DeliveryStatus.RetryScheduled) && x.NextAttemptAt <= now) ||
                     (x.Status == DeliveryStatus.Processing && x.ProcessingLeaseUntil <= now)))
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, DeliveryStatus.Processing)
                    .SetProperty(x => x.ProcessingLeaseUntil, leaseUntil), ct) == 1;
        }
        else
        {
            var candidate = await db.Deliveries.SingleOrDefaultAsync(x => x.Id == id, ct);
            claimed = candidate is not null &&
                (((candidate.Status == DeliveryStatus.Pending || candidate.Status == DeliveryStatus.RetryScheduled) && candidate.NextAttemptAt <= now) ||
                 (candidate.Status == DeliveryStatus.Processing && candidate.ProcessingLeaseUntil <= now));
            if (claimed) { candidate!.Status = DeliveryStatus.Processing; candidate.ProcessingLeaseUntil = leaseUntil; await db.SaveChangesAsync(ct); }
        }
        if (!claimed) return false;

        var delivery = await db.Deliveries.Include(x => x.Message).Include(x => x.Attempts).SingleAsync(x => x.Id == id, ct);
        var provider = providers.SingleOrDefault(x => string.Equals(x.Channel, delivery.Channel, StringComparison.OrdinalIgnoreCase));
        var started = DateTimeOffset.UtcNow;
        ProviderResult result;
        if (provider is null) result = new(false, "channel.unsupported", false);
        else
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(15));
            try { result = await provider.SendAsync(delivery.Message, timeout.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { result = new(false, "provider.timeout", true); }
            catch (Exception) { result = new(false, "provider.failure", true); }
        }

        delivery.AttemptCount++;
        delivery.ProviderResponseCode = result.Success ? result.Code : null;
        delivery.LastErrorCode = result.Success ? null : result.Code;
        delivery.ProcessingLeaseUntil = null;
        delivery.Attempts.Add(new DeliveryAttempt { AttemptNumber = delivery.AttemptCount, StartedAt = started, FinishedAt = DateTimeOffset.UtcNow, Succeeded = result.Success,
            ProviderResponseCode = result.Success ? result.Code : null, ErrorCode = result.Success ? null : result.Code, CorrelationId = delivery.Message.CorrelationId });
        NotificationTelemetry.Duration.Record((DateTimeOffset.UtcNow - started).TotalMilliseconds, new KeyValuePair<string, object?>("channel", delivery.Channel));
        if (result.Success) { delivery.Status = DeliveryStatus.Sent; delivery.SentAt = DateTimeOffset.UtcNow; delivery.NextAttemptAt = null; NotificationTelemetry.Sent.Add(1); }
        else if (DeliveryRetryPolicy.ShouldRetry(result.Transient, delivery.AttemptCount)) { delivery.Status = DeliveryStatus.RetryScheduled; delivery.NextAttemptAt = DeliveryRetryPolicy.NextAttempt(DateTimeOffset.UtcNow, delivery.AttemptCount); NotificationTelemetry.Retried.Add(1); }
        else { delivery.Status = DeliveryStatus.DeadLettered; delivery.NextAttemptAt = null; NotificationTelemetry.DeadLettered.Add(1); }
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public static class DeliveryRetryPolicy
{
    public const int MaxAttempts = 5;
    public static bool ShouldRetry(bool transient, int attemptCount) => transient && attemptCount < MaxAttempts;
    public static DateTimeOffset NextAttempt(DateTimeOffset now, int attemptCount) => now.AddSeconds(Math.Min(300, Math.Pow(2, attemptCount) * 5));
}
