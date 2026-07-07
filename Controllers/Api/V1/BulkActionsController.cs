using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawConnect.Services;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/bulk")]
[Produces("application/json")]
[Authorize]
public class BulkActionsController(
    IBulkDogActionService bulkDogActionService,
    IBulkNotificationOutboxActionService bulkNotificationOutboxActionService,
    IShelterService shelterService) : ControllerBase
{
    [HttpPost("shelter/dogs/status")]
    [Authorize(Roles = "Shelter")]
    [ProducesResponseType(typeof(BulkActionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BulkActionResultDto>> UpdateShelterDogStatuses(
        [FromBody] BulkDogStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var shelter = await shelterService.GetShelterForUserAsync(userId);
        if (shelter is null)
        {
            return BadRequest(new { message = "No shelter profile is linked to this account." });
        }

        try
        {
            return Ok(await bulkDogActionService.UpdateShelterDogStatusAsync(
                shelter.Id,
                userId,
                request.DogIds,
                request.NewStatus,
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("admin/notification-outbox")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(BulkActionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BulkActionResultDto>> UpdateNotificationOutbox(
        [FromBody] BulkNotificationOutboxRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            return request.Action.Trim().ToUpperInvariant() switch
            {
                "RETRY" => Ok(await bulkNotificationOutboxActionService.RetryAsync(userId, request.MessageIds, cancellationToken)),
                "CANCEL" => Ok(await bulkNotificationOutboxActionService.CancelAsync(userId, request.MessageIds, cancellationToken)),
                _ => BadRequest(new { message = "Unsupported notification outbox bulk action." })
            };
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
