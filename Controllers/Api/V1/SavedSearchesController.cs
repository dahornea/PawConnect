using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawConnect.Services;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/saved-searches")]
[Produces("application/json")]
[Authorize(Roles = "Adopter")]
public class SavedSearchesController(ISavedDogSearchService savedDogSearchService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SavedDogSearchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<SavedDogSearchDto>>> GetSavedSearches(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await savedDogSearchService.GetSavedSearchesForAdopterAsync(userId, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SavedDogSearchDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavedDogSearchDetailsDto>> GetSavedSearch(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var details = await savedDogSearchService.GetSavedSearchDetailsAsync(id, userId, cancellationToken);
        return details is null ? NotFound() : Ok(details);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(SavedDogSearchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SavedDogSearchDto>> CreateSavedSearch(
        [FromBody] SavedDogSearchCreateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var saved = await savedDogSearchService.CreateSavedSearchAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetSavedSearch), new { id = saved.Id }, saved);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(SavedDogSearchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SavedDogSearchDto>> UpdateSavedSearch(
        int id,
        [FromBody] SavedDogSearchUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await savedDogSearchService.UpdateSavedSearchAsync(id, userId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSavedSearch(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await savedDogSearchService.DeleteSavedSearchAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/evaluate")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(SavedDogSearchDetailsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SavedDogSearchDetailsDto>> EvaluateSavedSearch(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var details = await savedDogSearchService.EvaluateSavedSearchAsync(id, userId, cancellationToken);
        return details is null ? NotFound() : Ok(details);
    }

    [HttpPatch("{id:int}/alerts")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetAlerts(int id, [FromBody] SetSavedSearchAlertsRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await savedDogSearchService.SetAlertsAsync(id, userId, request.Enabled, cancellationToken);
        return NoContent();
    }

    [HttpPatch("matches/{matchId:int}/seen")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkMatchSeen(int matchId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await savedDogSearchService.MarkMatchAsSeenAsync(matchId, userId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("matches/{matchId:int}/dismiss")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DismissMatch(int matchId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await savedDogSearchService.DismissMatchAsync(matchId, userId, cancellationToken);
        return NoContent();
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}

public sealed record SetSavedSearchAlertsRequest(bool Enabled);
