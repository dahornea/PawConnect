using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawConnect.DTOs.Api;
using PawConnect.Services;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/notification-preferences")]
[Produces("application/json")]
[Authorize]
public class NotificationPreferencesController(
    INotificationPreferenceService notificationPreferenceService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationPreferenceApiDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<NotificationPreferenceApiDto>>> GetPreferences(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var preferences = await notificationPreferenceService.GetPreferencesAsync(userId, cancellationToken);
        return Ok(preferences.Select(ApiDtoMapper.ToNotificationPreference).ToList());
    }

    [HttpPut]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SavePreferences(
        [FromBody] IReadOnlyList<UpdateNotificationPreferenceApiRequest> request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await notificationPreferenceService.SavePreferencesAsync(
            userId,
            request
                .Select(update => new NotificationPreferenceUpdateDto(
                    update.NotificationType,
                    update.InAppEnabled,
                    update.EmailEnabled))
                .ToList(),
            cancellationToken);

        return NoContent();
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
