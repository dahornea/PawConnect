using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawConnect.Data;
using PawConnect.DTOs.Api;
using PawConnect.Services;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/favorites")]
[Produces("application/json")]
[Authorize(Roles = IdentitySeedData.AdopterRole)]
public sealed class FavoritesController(IFavoriteDogService favoriteDogService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DogListItemApiDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DogListItemApiDto>>> GetFavorites()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var favorites = await favoriteDogService.GetFavoritesForUserAsync(userId);
        return Ok(favorites
            .Where(favorite => favorite.Dog is not null)
            .Select(favorite => ApiDtoMapper.ToDogListItem(favorite.Dog!))
            .ToList());
    }

    [HttpGet("ids")]
    [ProducesResponseType(typeof(IReadOnlyList<int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<int>>> GetFavoriteIds()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var ids = await favoriteDogService.GetFavoriteDogIdsForUserAsync(userId);
        return Ok(ids.OrderBy(id => id).ToList());
    }

    [HttpPut("{dogId:int}")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddFavorite(int dogId)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            await favoriteDogService.AddFavoriteAsync(userId, dogId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpDelete("{dogId:int}")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveFavorite(int dogId)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        await favoriteDogService.RemoveFavoriteAsync(userId, dogId);
        return NoContent();
    }

    private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
