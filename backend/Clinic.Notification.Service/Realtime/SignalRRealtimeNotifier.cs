using Clinic.Notification.Service.Services.Delivery;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Clinic.Notification.Service.Realtime;

public sealed class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRRealtimeNotifier> _logger;

    public SignalRRealtimeNotifier(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendNotificationHintAsync(
        Guid userId,
        Guid notificationId,
        string notificationType,
        CancellationToken ct = default)
    {
        try
        {
            // Push only notification id, type, and timestamp hint - zero PHI, zero message body or credentials
            var hintPayload = new
            {
                id = notificationId,
                type = notificationType,
                timestamp = DateTimeOffset.UtcNow
            };

            await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotificationHint", hintPayload, ct);

            _logger.LogInformation("Realtime notification hint pushed to user {UserId} for notification {NotificationId}", userId, notificationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push realtime notification hint to user {UserId}", userId);
            throw;
        }
    }
}

