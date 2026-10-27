using Clinic.Notification.Service.Entities;
using Clinic.Notification.Service.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Clinic.Notification.Service.Endpoints;

public static class InternalNotificationEndpoints
{
    public static IEndpointRouteBuilder MapInternalNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/notifications");

        group.MapPost("/password-reset", async (
            PasswordResetNotificationRequest request,
            HttpRequest httpRequest,
            IConfiguration config,
            NotificationDbContext db,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("InternalNotificationEndpoints");

            // Service token authorization check
            var configuredToken = config["Identity:ServiceToken"] ?? "integration-test-service-token";
            if (!httpRequest.Headers.TryGetValue("X-Clinic-Service-Token", out var providedToken) ||
                providedToken != configuredToken)
            {
                return Results.Unauthorized();
            }

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

            // Secure template rendering: reset code is isolated to the delivery outbox payload
            var content = $"Your password reset code is {request.ResetToken}. It will expire in 30 minutes. If you did not request this, please ignore.";

            var outboxMessage = new DeliveryOutboxMessage
            {
                Id = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                RecipientId = request.RecipientId,
                RecipientType = RecipientType.Account,
                Channel = channel,
                NotificationType = NotificationType.PasswordReset,
                IdempotencyKey = $"reset:{request.RecipientId}:{Guid.NewGuid():N}",
                RecipientAddress = targetAddress,
                Content = content,
                Status = DeliveryStatus.Pending,
                AttemptsCount = 0,
                MaxAttempts = 3,
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.DeliveryOutboxMessages.Add(outboxMessage);
            await db.SaveChangesAsync(ct);

            // Audit log without leaking the secret reset token
            logger.LogInformation("Password reset notification enqueued for RecipientId: {RecipientId}, Channel: {Channel}",
                request.RecipientId, channel);

            return Results.Ok(new { enqueued = true, deliveryId = outboxMessage.Id });
        });

        return app;
    }
}

public sealed record PasswordResetNotificationRequest(
    Guid RecipientId,
    string? Email,
    string? PhoneNumber,
    string ResetToken
);

