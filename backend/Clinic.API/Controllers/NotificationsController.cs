using Clinic.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers;

[ApiController]
[Route("/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly NotificationServiceClient _notificationClient;

    public NotificationsController(NotificationServiceClient notificationClient)
    {
        _notificationClient = notificationClient;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        return await _notificationClient.ProxyAsync(HttpContext, "notifications", HttpMethod.Get);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        return await _notificationClient.ProxyAsync(HttpContext, "notifications/unread-count", HttpMethod.Get);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead([FromRoute] Guid id)
    {
        return await _notificationClient.ProxyAsync(HttpContext, $"notifications/{id}/read", HttpMethod.Post);
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsReadPut([FromRoute] Guid id)
    {
        return await _notificationClient.ProxyAsync(HttpContext, $"notifications/{id}/read", HttpMethod.Put);
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        return await _notificationClient.ProxyAsync(HttpContext, "notifications/read-all", HttpMethod.Post);
    }
}
