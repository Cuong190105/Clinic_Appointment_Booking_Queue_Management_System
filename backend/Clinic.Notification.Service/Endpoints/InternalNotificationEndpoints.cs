using System.Security.Cryptography;
using System.Text;
using Clinic.Notification.Service.Entities;
using Clinic.Notification.Service.Persistence;
using Clinic.Notification.Service.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Clinic.Notification.Service.Endpoints;

public static class InternalNotificationEndpoints
{
    public const string ServiceTokenHeader = "X-Clinic-Service-Token";

    public static IEndpointRouteBuilder MapInternalNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/notifications");

        group.MapPost("/password-reset", async (
            PasswordResetNotificationRequest request,
            HttpRequest httpRequest,
            IConfiguration config,
            NotificationDbContext db,
            ISecretProtector protector,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("InternalNotificationEndpoints");

            var auth = Authorize(httpRequest, config, logger);
            if (auth != null) return auth;

            // Input validation
            if (request.RecipientId == Guid.Empty ||
                string.IsNullOrWhiteSpace(request.ResetToken) ||
                (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.PhoneNumber)))
            {
                return Results.BadRequest(new { error = "Invalid password reset notification request." });
            }

            var channel = !string.IsNullOrWhiteSpace(request.Email)
                ? NotificationChannel.Email
                : NotificationChannel.Sms;

            var targetAddress = (request.Email ?? request.PhoneNumber)!.Trim();

            // Stable idempotency key bound to the reset request (recipient + hash of the reset code):
            // a replayed request never creates a second delivery and the code itself is not stored in the key.
            var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.ResetToken)))[..24];
            var idempotencyKey = $"reset:{request.RecipientId:N}:{tokenHash}";

            var existing = await db.DeliveryOutboxMessages.AsNoTracking()
                .Where(o => o.IdempotencyKey == idempotencyKey)
                .Select(o => o.Id)
                .FirstOrDefaultAsync(ct);
            if (existing != Guid.Empty)
            {
                return Results.Ok(new { enqueued = true, deliveryId = existing, duplicate = true });
            }

            // The reset code is encrypted at rest (key ring is outside the database) and redacted after delivery/expiry.
            var content = protector.Protect(
                $"Your password reset code is {request.ResetToken}. It will expire in 30 minutes. If you did not request this, please ignore.");

            var outboxMessage = new DeliveryOutboxMessage
            {
                Id = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                RecipientId = request.RecipientId,
                RecipientType = RecipientType.Account,
                Channel = channel,
                NotificationType = NotificationType.PasswordReset,
                IdempotencyKey = idempotencyKey,
                RecipientAddress = targetAddress,
                Subject = "Password reset",
                Content = content,
                Status = DeliveryStatus.Pending,
                AttemptsCount = 0,
                MaxAttempts = 3,
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.DeliveryOutboxMessages.Add(outboxMessage);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Lost a race with an identical concurrent request: treat as duplicate.
                var raced = await db.DeliveryOutboxMessages.AsNoTracking()
                    .Where(o => o.IdempotencyKey == idempotencyKey)
                    .Select(o => o.Id)
                    .FirstOrDefaultAsync(ct);
                if (raced == Guid.Empty) throw;
                return Results.Ok(new { enqueued = true, deliveryId = raced, duplicate = true });
            }

            // Audit log without leaking the secret reset token
            logger.LogInformation("Password reset notification enqueued for RecipientId: {RecipientId}, Channel: {Channel}",
                request.RecipientId, channel);

            return Results.Ok(new { enqueued = true, deliveryId = outboxMessage.Id });
        });

        // Operator view of dead-lettered deliveries. Never returns content, addresses or secrets.
        group.MapGet("/deliveries/dead-lettered", async (
            HttpRequest httpRequest,
            IConfiguration config,
            NotificationDbContext db,
            ILoggerFactory loggerFactory,
            int? limit,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("InternalNotificationEndpoints");
            var auth = Authorize(httpRequest, config, logger);
            if (auth != null) return auth;
            if (!TryGetOperatorId(httpRequest, out var operatorId)) return Results.Unauthorized();

            var take = Math.Clamp(limit ?? 50, 1, 200);
            var items = await db.DeliveryOutboxMessages.AsNoTracking()
                .Where(o => o.Status == DeliveryStatus.DeadLettered)
                .OrderByDescending(o => o.CreatedAt)
                .Take(take)
                .Select(o => new
                {
                    o.Id,
                    Type = o.NotificationType.ToString(),
                    Channel = o.Channel.ToString(),
                    o.AttemptsCount,
                    o.CreatedAt,
                    Replayable = o.NotificationType != NotificationType.PasswordReset
                })
                .ToListAsync(ct);

            var ids = items.Select(i => i.Id).ToList();
            var attempts = items.Count == 0
                ? new Dictionary<Guid, string?>()
                : (await db.DeliveryAttempts.AsNoTracking()
                    .Where(a => ids.Contains(a.DeliveryOutboxMessageId))
                    .OrderByDescending(a => a.AttemptNumber)
                    .Select(a => new { a.DeliveryOutboxMessageId, a.ProviderResponse })
                    .ToListAsync(ct))
                    .GroupBy(a => a.DeliveryOutboxMessageId)
                    .ToDictionary(g => g.Key, g => (string?)g.First().ProviderResponse);

            logger.LogInformation("AUDIT: operator {OperatorId} listed dead-lettered notification deliveries.", operatorId);
            return Results.Ok(items.Select(i => new
            {
                i.Id,
                i.Type,
                i.Channel,
                i.AttemptsCount,
                i.CreatedAt,
                i.Replayable,
                LastFailureReason = attempts.TryGetValue(i.Id, out var reason) ? reason : null
            }));
        });

        // Deliberate, audited replay of a dead-lettered delivery. Password-reset secrets are never replayable.
        group.MapPost("/deliveries/{id:guid}/replay", async (
            Guid id,
            HttpRequest httpRequest,
            IConfiguration config,
            NotificationDbContext db,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("InternalNotificationEndpoints");
            var auth = Authorize(httpRequest, config, logger);
            if (auth != null) return auth;
            if (!TryGetOperatorId(httpRequest, out var operatorId)) return Results.Unauthorized();

            var delivery = await db.DeliveryOutboxMessages.FirstOrDefaultAsync(o => o.Id == id, ct);
            if (delivery == null) return Results.NotFound(new { error = "Delivery not found." });
            if (delivery.Status != DeliveryStatus.DeadLettered)
                return Results.Conflict(new { error = "Only dead-lettered deliveries can be replayed." });
            if (delivery.NotificationType == NotificationType.PasswordReset)
                return Results.Conflict(new { error = "Password-reset deliveries are not replayable; request a new reset." });

            delivery.Status = DeliveryStatus.Pending;
            delivery.AttemptsCount = 0;
            delivery.NextAttemptAt = DateTimeOffset.UtcNow;
            db.DeliveryAttempts.Add(new DeliveryAttempt
            {
                Id = Guid.NewGuid(),
                DeliveryOutboxMessageId = delivery.Id,
                AttemptNumber = 0,
                AttemptedAt = DateTimeOffset.UtcNow,
                Status = DeliveryStatus.Pending,
                ProviderResponse = "Operator replay requested.",
                CorrelationId = delivery.CorrelationId,
                OperatorId = operatorId
            });
            await db.SaveChangesAsync(ct);

            logger.LogWarning("AUDIT: dead-lettered delivery {DeliveryId} replayed by operator {OperatorId}.", delivery.Id, operatorId);
            return Results.Ok(new { replayed = true, deliveryId = delivery.Id });
        });

        return app;
    }

    /// <summary>
    /// Returns null when the caller presented the configured service token; otherwise an error result.
    /// The token is mandatory configuration: when missing the endpoint refuses commands instead of using a default.
    /// </summary>
    private static IResult? Authorize(HttpRequest request, IConfiguration config, ILogger logger)
    {
        var configured = config["Identity:ServiceToken"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            logger.LogError("Identity:ServiceToken is not configured; refusing internal notification command.");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (!request.Headers.TryGetValue(ServiceTokenHeader, out var provided) ||
            !FixedTimeEquals(provided.ToString(), configured))
        {
            return Results.Unauthorized();
        }

        return null;
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ha = SHA256.HashData(Encoding.UTF8.GetBytes(a));
        var hb = SHA256.HashData(Encoding.UTF8.GetBytes(b));
        return CryptographicOperations.FixedTimeEquals(ha, hb);
    }

    private static bool TryGetOperatorId(HttpRequest request, out Guid operatorId) =>
        Guid.TryParse(request.Headers["X-Clinic-Operator-Id"].ToString(), out operatorId);
}

public sealed record PasswordResetNotificationRequest(
    Guid RecipientId,
    string? Email,
    string? PhoneNumber,
    string ResetToken
);
