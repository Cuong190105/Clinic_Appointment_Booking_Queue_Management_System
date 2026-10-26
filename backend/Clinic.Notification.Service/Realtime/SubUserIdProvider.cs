using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Clinic.Notification.Service.Realtime;

public sealed class SubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        // Strictly extract authenticated Identity subject, ignoring any client-supplied parameters
        var sub = connection.User?.FindFirst("sub")?.Value 
            ?? connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return sub;
    }
}
