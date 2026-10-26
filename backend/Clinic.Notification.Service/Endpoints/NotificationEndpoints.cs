using System.Security.Claims;
using Clinic.Notification.Service.Entities;
using Clinic.Notification.Service.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Notification.Service.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notifications")
            .RequireAuthorization();

        group.MapGet("/", GetNotificationsAsync);
        group.MapGet("/me", GetNotificationsAsync);
        group.MapGet("/unread-count", GetUnreadCountAsync);
        group.MapPost("/{id:guid}/read", MarkAsReadAsync);
        group.MapPut("/{id:guid}/read", MarkAsReadAsync);
        group.MapPost("/read-all", MarkAllAsReadAsync);

        // Alias for Gateway proxy compatibility
        app.MapGet("/me/notifications", GetNotificationsAsync)
            .RequireAuthorization();

        return app;
    }

    private static bool TryGetAuthenticatedUserId(ClaimsPrincipal user, out Guid userId)
    {
        var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private static async Task<IResult> GetNotificationsAsync(
        ClaimsPrincipal user,
        NotificationDbContext db,
        DateTimeOffset? createdBefore,
        int? limit,
        int? page,
        int? pageSize,
        bool? unreadOnly,
        CancellationToken ct)
    {
        if (!TryGetAuthenticatedUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        // History API is strictly scoped to the authenticated user account.
        // Guest contact-only records have RecipientType.Guest and are never exposed here.
        var query = db.Notifications.AsNoTracking()
            .Where(n => n.RecipientId == userId && n.RecipientType == RecipientType.Account);

        if (unreadOnly == true)
        {
            query = query.Where(n => !n.IsRead);
        }

        if (createdBefore.HasValue)
        {
            query = query.Where(n => n.CreatedAt < createdBefore.Value);
        }

        var totalCount = await query.CountAsync(ct);

        query = query.OrderByDescending(n => n.CreatedAt);

        var take = limit ?? pageSize ?? 20;
        if (take < 1) take = 20;
        if (take > 100) take = 100;

        if (page.HasValue && page.Value > 1)
        {
            query = query.Skip((page.Value - 1) * take);
        }

        var items = await query.Take(take)
            .Select(n => new NotificationItemDto(
                n.Id,
                n.Title,
                n.Body,
                n.IsRead,
                n.CreatedAt,
                n.ReadAt,
                n.NotificationType.ToString(),
                "InApp"
            ))
            .ToListAsync(ct);

        return Results.Ok(new NotificationListResponse(items, totalCount));
    }

    private static async Task<IResult> GetUnreadCountAsync(
        ClaimsPrincipal user,
        NotificationDbContext db,
        CancellationToken ct)
    {
        if (!TryGetAuthenticatedUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var count = await db.Notifications.AsNoTracking()
            .CountAsync(n => n.RecipientId == userId && n.RecipientType == RecipientType.Account && !n.IsRead, ct);

        return Results.Ok(new { unreadCount = count });
    }

    private static async Task<IResult> MarkAsReadAsync(
        Guid id,
        ClaimsPrincipal user,
        NotificationDbContext db,
        CancellationToken ct)
    {
        if (!TryGetAuthenticatedUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (notification == null)
        {
            return Results.NotFound(new { message = "Notification not found" });
        }

        // IDOR protection: cannot read or mutate another user's or guest notification
        if (notification.RecipientId != userId || notification.RecipientType != RecipientType.Account)
        {
            return Results.Json(new { message = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(new { id = notification.Id, isRead = true });
    }

    private static async Task<IResult> MarkAllAsReadAsync(
        ClaimsPrincipal user,
        NotificationDbContext db,
        CancellationToken ct)
    {
        if (!TryGetAuthenticatedUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var unreadNotifications = await db.Notifications
            .Where(n => n.RecipientId == userId && n.RecipientType == RecipientType.Account && !n.IsRead)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var notif in unreadNotifications)
        {
            notif.IsRead = true;
            notif.ReadAt = now;
        }

        if (unreadNotifications.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(new { markedCount = unreadNotifications.Count });
    }
}

public sealed record NotificationItemDto(
    Guid Id,
    string Title,
    string Message,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    string Type,
    string Channel
);

public sealed record NotificationListResponse(
    IReadOnlyList<NotificationItemDto> Items,
    int TotalCount
);
