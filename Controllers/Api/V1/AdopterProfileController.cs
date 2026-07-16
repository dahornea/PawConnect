using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawConnect.Data;
using PawConnect.DTOs.Api;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/adopter/profile")]
[Produces("application/json")]
[Authorize(Roles = IdentitySeedData.AdopterRole)]
public sealed class AdopterProfileController(IAdopterProfileService adopterProfileService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AdopterProfileApiDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdopterProfileApiDto>> GetProfile()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var profile = await adopterProfileService.GetProfileForUserAsync(userId);
        return Ok(profile is null
            ? new AdopterProfileApiDto(
                string.Empty,
                null,
                null,
                string.Empty,
                null,
                HousingType.Apartment,
                false,
                false,
                false,
                null,
                null)
            : ToDto(profile));
    }

    [HttpPut]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(AdopterProfileApiDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdopterProfileApiDto>> UpdateProfile(
        [FromBody] UpdateAdopterProfileApiRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var profile = new AdopterProfile
        {
            FullName = request.FullName,
            ProfileImageUrl = request.ProfileImageUrl,
            Address = request.Address,
            City = request.City,
            PhoneNumber = request.PhoneNumber,
            HousingType = request.HousingType,
            HasYard = request.HasYard,
            HasOtherPets = request.HasOtherPets,
            HasChildren = request.HasChildren,
            ExperienceWithDogs = request.ExperienceWithDogs,
            AdditionalNotes = request.AdditionalNotes
        };

        try
        {
            await adopterProfileService.CreateOrUpdateProfileAsync(userId, profile);
            var saved = await adopterProfileService.GetProfileForUserAsync(userId);
            return saved is null ? NotFound() : Ok(ToDto(saved));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    private static AdopterProfileApiDto ToDto(AdopterProfile profile) => new(
        profile.FullName,
        profile.ProfileImageUrl,
        profile.Address,
        profile.City,
        profile.PhoneNumber,
        profile.HousingType,
        profile.HasYard,
        profile.HasOtherPets,
        profile.HasChildren,
        profile.ExperienceWithDogs,
        profile.AdditionalNotes);

    private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
