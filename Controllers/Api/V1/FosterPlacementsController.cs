using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawConnect.Data;
using PawConnect.DTOs.Api;
using PawConnect.Services;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/foster-placements")]
[Produces("application/json")]
[Authorize(Roles = $"{IdentitySeedData.ShelterRole},{IdentitySeedData.AdminRole}")]
public class FosterPlacementsController(
    IFosterPlacementService fosterPlacementService,
    IShelterService shelterService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FosterPlacementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<FosterPlacementDto>>> GetPlacements(
        [FromQuery] FosterPlacementFilterDto filter)
    {
        if (User.IsInRole(IdentitySeedData.AdminRole))
        {
            return Ok(await fosterPlacementService.GetAdminPlacementsAsync(filter));
        }

        var shelterId = await GetCurrentShelterIdAsync();
        return shelterId is null
            ? BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."))
            : Ok(await fosterPlacementService.GetShelterPlacementsAsync(shelterId.Value, filter));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FosterPlacementDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FosterPlacementDetailsDto>> GetPlacement(int id)
    {
        var isAdmin = User.IsInRole(IdentitySeedData.AdminRole);
        var shelterId = isAdmin ? null : await GetCurrentShelterIdAsync();
        var placement = await fosterPlacementService.GetPlacementDetailsAsync(id, shelterId, isAdmin);
        return placement is null
            ? NotFound(new ApiErrorResponse("Foster placement was not found or is not visible to this account."))
            : Ok(placement);
    }

    [HttpPost]
    [Authorize(Roles = IdentitySeedData.ShelterRole)]
    [ProducesResponseType(typeof(FosterPlacementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FosterPlacementDto>> CreatePlacement(
        [FromBody] FosterPlacementCreateRequest request)
    {
        var userId = GetCurrentUserId();
        var shelterId = await GetCurrentShelterIdAsync();
        if (string.IsNullOrWhiteSpace(userId) || shelterId is null)
        {
            return BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."));
        }

        try
        {
            var created = await fosterPlacementService.CreatePlacementAsync(shelterId.Value, userId, request);
            return CreatedAtAction(nameof(GetPlacement), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = IdentitySeedData.ShelterRole)]
    [ProducesResponseType(typeof(FosterPlacementDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FosterPlacementDetailsDto>> UpdatePlacement(
        int id,
        [FromBody] FosterPlacementUpdateRequest request)
    {
        var userId = GetCurrentUserId();
        var shelterId = await GetCurrentShelterIdAsync();
        if (string.IsNullOrWhiteSpace(userId) || shelterId is null)
        {
            return BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."));
        }

        try
        {
            return Ok(await fosterPlacementService.UpdatePlacementAsync(id, shelterId.Value, userId, request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPatch("{id:int}/approve")]
    [Authorize(Roles = IdentitySeedData.ShelterRole)]
    [ProducesResponseType(typeof(FosterPlacementDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<ActionResult<FosterPlacementDetailsDto>> ApprovePlacement(
        int id,
        [FromBody] FosterPlacementDecisionRequest? request)
    {
        return RunShelterActionAsync((shelterId, userId) =>
            fosterPlacementService.ApprovePlacementAsync(id, shelterId, userId, request));
    }

    [HttpPatch("{id:int}/start")]
    [Authorize(Roles = IdentitySeedData.ShelterRole)]
    [ProducesResponseType(typeof(FosterPlacementDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<ActionResult<FosterPlacementDetailsDto>> StartPlacement(
        int id,
        [FromBody] FosterPlacementStartRequest? request)
    {
        return RunShelterActionAsync((shelterId, userId) =>
            fosterPlacementService.StartPlacementAsync(id, shelterId, userId, request));
    }

    [HttpPatch("{id:int}/complete")]
    [ProducesResponseType(typeof(FosterPlacementDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FosterPlacementDetailsDto>> CompletePlacement(
        int id,
        [FromBody] FosterPlacementCompleteRequest request)
    {
        return await RunAdminOrShelterActionAsync((userId, shelterId, isAdmin) =>
            fosterPlacementService.CompletePlacementAsync(id, shelterId, userId, isAdmin, request));
    }

    [HttpPatch("{id:int}/cancel")]
    [ProducesResponseType(typeof(FosterPlacementDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<ActionResult<FosterPlacementDetailsDto>> CancelPlacement(
        int id,
        [FromBody] FosterPlacementDecisionRequest? request)
    {
        return RunAdminOrShelterActionAsync((userId, shelterId, isAdmin) =>
            fosterPlacementService.CancelPlacementAsync(id, shelterId, userId, isAdmin, request));
    }

    [HttpGet("~/api/v1/dogs/{dogId:int}/foster-history")]
    [ProducesResponseType(typeof(IReadOnlyList<DogFosterHistoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<DogFosterHistoryItemDto>>> GetDogFosterHistory(int dogId)
    {
        var isAdmin = User.IsInRole(IdentitySeedData.AdminRole);
        var shelterId = isAdmin ? null : await GetCurrentShelterIdAsync();
        return Ok(await fosterPlacementService.GetDogFosterHistoryAsync(dogId, shelterId, isAdmin));
    }

    [HttpGet("~/api/v1/foster-caregivers")]
    [ProducesResponseType(typeof(IReadOnlyList<FosterCaregiverProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<FosterCaregiverProfileDto>>> GetCaregivers()
    {
        if (User.IsInRole(IdentitySeedData.AdminRole))
        {
            return Ok(await fosterPlacementService.GetAvailableCaregiversAsync());
        }

        var shelterId = await GetCurrentShelterIdAsync();
        return shelterId is null
            ? BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."))
            : Ok(await fosterPlacementService.GetCaregiversForShelterAsync(shelterId.Value));
    }

    [HttpPost("~/api/v1/foster-caregivers")]
    [Authorize(Roles = IdentitySeedData.ShelterRole)]
    [ProducesResponseType(typeof(FosterCaregiverProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FosterCaregiverProfileDto>> CreateCaregiver(
        [FromBody] FosterCaregiverCreateRequest request)
    {
        var userId = GetCurrentUserId();
        var shelterId = await GetCurrentShelterIdAsync();
        if (string.IsNullOrWhiteSpace(userId) || shelterId is null)
        {
            return BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."));
        }

        try
        {
            var created = await fosterPlacementService.CreateCaregiverAsync(request, userId, shelterId.Value);
            return CreatedAtAction(nameof(GetCaregivers), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPut("~/api/v1/foster-caregivers/{id:int}")]
    [ProducesResponseType(typeof(FosterCaregiverProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FosterCaregiverProfileDto>> UpdateCaregiver(
        int id,
        [FromBody] FosterCaregiverUpdateRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var isAdmin = User.IsInRole(IdentitySeedData.AdminRole);
        var shelterId = isAdmin ? null : await GetCurrentShelterIdAsync();
        if (!isAdmin && shelterId is null)
        {
            return BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."));
        }

        try
        {
            return Ok(await fosterPlacementService.UpdateCaregiverAsync(id, request, userId, shelterId, isAdmin));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    private async Task<ActionResult<FosterPlacementDetailsDto>> RunShelterActionAsync(
        Func<int, string, Task<FosterPlacementDetailsDto>> action)
    {
        var userId = GetCurrentUserId();
        var shelterId = await GetCurrentShelterIdAsync();
        if (string.IsNullOrWhiteSpace(userId) || shelterId is null)
        {
            return BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."));
        }

        try
        {
            return Ok(await action(shelterId.Value, userId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    private async Task<ActionResult<FosterPlacementDetailsDto>> RunAdminOrShelterActionAsync(
        Func<string, int?, bool, Task<FosterPlacementDetailsDto>> action)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var isAdmin = User.IsInRole(IdentitySeedData.AdminRole);
        var shelterId = isAdmin ? null : await GetCurrentShelterIdAsync();
        if (!isAdmin && shelterId is null)
        {
            return BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."));
        }

        try
        {
            return Ok(await action(userId, shelterId, isAdmin));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    private async Task<int?> GetCurrentShelterIdAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var shelter = await shelterService.GetShelterForUserAsync(userId);
        return shelter?.Id;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
