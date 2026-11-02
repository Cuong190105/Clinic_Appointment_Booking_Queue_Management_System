using Microsoft.AspNetCore.SignalR;

namespace Clinic.API.Hubs
{
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class NotificationHub : Hub
    {
    }
}
