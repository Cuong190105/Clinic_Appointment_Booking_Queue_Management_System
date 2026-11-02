using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Clinic.Notification.Service.Realtime;

[Authorize]
public sealed class NotificationHub : Hub
{
    // Realtime notifications are pushed server-to-client as hints.
    // SignalR groups connections automatically by authenticated Identity subject via IUserIdProvider.
}

