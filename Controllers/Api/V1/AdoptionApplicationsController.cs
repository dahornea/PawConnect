using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawConnect.Data;
using PawConnect.DTOs.Api;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/adoption-applications")]
[Produces("application/json")]
[Authorize(Roles = $"{IdentitySeedData.AdopterRole},{IdentitySeedData.ShelterRole},{IdentitySeedData.AdminRole}")]
public class AdoptionApplicationsController(
    IAdoptionRequestService adoptionRequestService,
    IShelterService shelterService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiPagedResult<AdoptionApplicationApiDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiPagedResult<AdoptionApplicationApiDto>>> GetApplications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var requests = await GetRequestsForCurrentUserAsync(userId);

        return Ok(ApiPagedResult<AdoptionApplicationApiDto>.Create(
            requests.Select(ApiDtoMapper.ToAdoptionApplication),
            page,
            pageSize));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AdoptionApplicationApiDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdoptionApplicationApiDto>> GetApplication(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var request = await adoptionRequestService.GetByIdAsync(id);
        if (request is null)
        {
            return NotFound(new ApiErrorResponse("Adoption application was not found."));
        }

        return await CanCurrentUserAccessRequestAsync(userId, request)
            ? Ok(ApiDtoMapper.ToAdoptionApplication(request))
            : Forbid();
    }

    [HttpPost("~/api/v1/dogs/{dogId:int}/adoption-applications")]
    [Authorize(Roles = IdentitySeedData.AdopterRole)]
    [ProducesResponseType(typeof(AdoptionApplicationApiDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdoptionApplicationApiDto>> CreateApplication(
        int dogId,
        [FromBody] CreateAdoptionApplicationApiRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            await adoptionRequestService.CreateRequestAsync(
                userId,
                dogId,
                new AdoptionRequestQuestionnaire(
                    request.ReasonForAdoption,
                    request.HoursAlonePerDay,
                    request.AdditionalInformation,
                    request.PreferredVisitDateTime));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }

        var created = (await adoptionRequestService.GetRequestsForAdopterAsync(userId))
            .FirstOrDefault(application => application.DogId == dogId);

        if (created is null)
        {
            return StatusCode(
                StatusCodes.Status201Created,
                new ApiErrorResponse("Adoption application was created, but the created record could not be reloaded."));
        }

        var dto = ApiDtoMapper.ToAdoptionApplication(created);
        return CreatedAtAction(nameof(GetApplication), new { id = dto.Id }, dto);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = IdentitySeedData.AdopterRole)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelApplication(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            await adoptionRequestService.CancelRequestAsync(id, userId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }

        return NoContent();
    }

    private async Task<List<AdoptionRequest>> GetRequestsForCurrentUserAsync(string userId)
    {
        if (User.IsInRole(IdentitySeedData.AdminRole))
        {
            return await adoptionRequestService.GetAllAsync();
        }

        if (User.IsInRole(IdentitySeedData.ShelterRole))
        {
            var shelter = await shelterService.GetShelterForUserAsync(userId);
            return shelter is null
                ? []
                : await adoptionRequestService.GetRequestsForShelterAsync(shelter.Id);
        }

        return await adoptionRequestService.GetRequestsForAdopterAsync(userId);
    }

    private async Task<bool> CanCurrentUserAccessRequestAsync(string userId, AdoptionRequest request)
    {
        if (User.IsInRole(IdentitySeedData.AdminRole))
        {
            return true;
        }

        if (User.IsInRole(IdentitySeedData.AdopterRole))
        {
            return request.AdopterId == userId;
        }

        if (!User.IsInRole(IdentitySeedData.ShelterRole))
        {
            return false;
        }

        var shelter = await shelterService.GetShelterForUserAsync(userId);
        return shelter is not null && request.Dog?.ShelterId == shelter.Id;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}


