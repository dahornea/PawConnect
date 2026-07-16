using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawConnect.Data;
using PawConnect.DTOs.Api;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/notifications")]
[Produces("application/json")]
[Authorize(Roles = IdentitySeedData.AdopterRole)]
public sealed class NotificationsController(INotificationCenterService notificationCenterService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(NotificationCenterResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationCenterResultDto>> GetNotifications(
        [FromQuery] NotificationCategory? category,
        [FromQuery] NotificationReadState readState = NotificationReadState.All,
        [FromQuery] string? search = null,
        [FromQuery] int count = 100,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        return Ok(await notificationCenterService.GetNotificationsAsync(
            userId,
            new NotificationCenterQuery(category, readState, search, count),
            cancellationToken));
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(NotificationUnreadCountApiDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationUnreadCountApiDto>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        return Ok(new NotificationUnreadCountApiDto(
            await notificationCenterService.GetUnreadCountAsync(userId, cancellationToken)));
    }

    [HttpPatch("{id:int}/read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        await notificationCenterService.MarkAsReadAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:int}/unread")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkUnread(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        await notificationCenterService.MarkAsUnreadAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("read-all")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        await notificationCenterService.MarkAllAsReadAsync(userId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        await notificationCenterService.DismissAsync(id, userId, cancellationToken);
        return NoContent();
    }

    private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
