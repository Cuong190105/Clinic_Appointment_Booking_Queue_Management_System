using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
// Keep the same full hub type name as the Gateway so both processes share
// the same SignalR Redis-backplane channel.
namespace Clinic.API.Hubs;
[Authorize]
public sealed class NotificationHub : Hub { }
