using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawConnect.Data;
using PawConnect.DTOs.Api;
using PawConnect.Services;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/transfers")]
[Produces("application/json")]
[Authorize(Roles = $"{IdentitySeedData.ShelterRole},{IdentitySeedData.AdminRole}")]
public class TransfersController(
    IDogTransferService dogTransferService,
    IShelterService shelterService) : ControllerBase
{
    [HttpGet("incoming")]
    [Authorize(Roles = IdentitySeedData.ShelterRole)]
    [ProducesResponseType(typeof(IReadOnlyList<DogTransferRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<DogTransferRequestDto>>> GetIncomingTransfers()
    {
        var shelterId = await GetCurrentShelterIdAsync();
        return shelterId is null
            ? BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."))
            : Ok(await dogTransferService.GetIncomingTransfersAsync(shelterId.Value));
    }

    [HttpGet("outgoing")]
    [Authorize(Roles = IdentitySeedData.ShelterRole)]
    [ProducesResponseType(typeof(IReadOnlyList<DogTransferRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<DogTransferRequestDto>>> GetOutgoingTransfers()
    {
        var shelterId = await GetCurrentShelterIdAsync();
        return shelterId is null
            ? BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."))
            : Ok(await dogTransferService.GetOutgoingTransfersAsync(shelterId.Value));
    }

    [HttpGet]
    [Authorize(Roles = IdentitySeedData.AdminRole)]
    [ProducesResponseType(typeof(IReadOnlyList<DogTransferRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<DogTransferRequestDto>>> GetAdminTransfers(
        [FromQuery] DogTransferFilterDto filter)
    {
        return Ok(await dogTransferService.GetAdminTransfersAsync(filter));
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(DogTransferStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DogTransferStatsDto>> GetTransferStats()
    {
        if (User.IsInRole(IdentitySeedData.AdminRole))
        {
            return Ok(await dogTransferService.GetTransferStatsAsync());
        }

        var shelterId = await GetCurrentShelterIdAsync();
        return shelterId is null
            ? BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."))
            : Ok(await dogTransferService.GetTransferStatsAsync(shelterId.Value));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DogTransferDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DogTransferDetailsDto>> GetTransfer(int id)
    {
        var isAdmin = User.IsInRole(IdentitySeedData.AdminRole);
        var shelterId = isAdmin ? null : await GetCurrentShelterIdAsync();
        var transfer = await dogTransferService.GetTransferDetailsAsync(id, shelterId, isAdmin);
        return transfer is null
            ? NotFound(new ApiErrorResponse("Transfer request was not found or is not visible to this account."))
            : Ok(transfer);
    }

    [HttpPost]
    [Authorize(Roles = IdentitySeedData.ShelterRole)]
    [ProducesResponseType(typeof(DogTransferRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DogTransferRequestDto>> CreateTransfer(
        [FromBody] DogTransferCreateRequest request)
    {
        var userId = GetCurrentUserId();
        var shelterId = await GetCurrentShelterIdAsync();
        if (string.IsNullOrWhiteSpace(userId) || shelterId is null)
        {
            return BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."));
        }

        try
        {
            var created = await dogTransferService.CreateTransferRequestAsync(shelterId.Value, userId, request);
            return CreatedAtAction(nameof(GetTransfer), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPatch("{id:int}/approve")]
    [Authorize(Roles = IdentitySeedData.ShelterRole)]
    [ProducesResponseType(typeof(DogTransferDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DogTransferDetailsDto>> ApproveTransfer(
        int id,
        [FromBody] DogTransferDecisionRequest request)
    {
        return await RespondToTransferAsync(id, request, approve: true);
    }

    [HttpPatch("{id:int}/reject")]
    [Authorize(Roles = IdentitySeedData.ShelterRole)]
    [ProducesResponseType(typeof(DogTransferDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DogTransferDetailsDto>> RejectTransfer(
        int id,
        [FromBody] DogTransferDecisionRequest request)
    {
        return await RespondToTransferAsync(id, request, approve: false);
    }

    [HttpPatch("{id:int}/cancel")]
    [ProducesResponseType(typeof(DogTransferDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DogTransferDetailsDto>> CancelTransfer(
        int id,
        [FromBody] DogTransferDecisionRequest? request)
    {
        return await RunTransferActionAsync((userId, shelterId, isAdmin) =>
            dogTransferService.CancelTransferAsync(id, shelterId, userId, isAdmin, request));
    }

    [HttpPatch("{id:int}/complete")]
    [ProducesResponseType(typeof(DogTransferDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DogTransferDetailsDto>> CompleteTransfer(
        int id,
        [FromBody] DogTransferCompleteRequest? request)
    {
        return await RunTransferActionAsync((userId, shelterId, isAdmin) =>
            dogTransferService.CompleteTransferAsync(id, shelterId, userId, isAdmin, request));
    }

    [HttpPatch("{id:int}/admin-note")]
    [Authorize(Roles = IdentitySeedData.AdminRole)]
    [ProducesResponseType(typeof(DogTransferDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DogTransferDetailsDto>> UpdateAdminNote(
        int id,
        [FromBody] DogTransferAdminNoteRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await dogTransferService.UpdateAdminNotesAsync(id, request.AdminNotes, userId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpGet("~/api/v1/dogs/{dogId:int}/transfers")]
    [ProducesResponseType(typeof(IReadOnlyList<DogTransferHistoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<DogTransferHistoryItemDto>>> GetDogTransferHistory(int dogId)
    {
        var isAdmin = User.IsInRole(IdentitySeedData.AdminRole);
        var shelterId = isAdmin ? null : await GetCurrentShelterIdAsync();
        return Ok(await dogTransferService.GetDogTransferHistoryAsync(dogId, shelterId, isAdmin));
    }

    private async Task<ActionResult<DogTransferDetailsDto>> RespondToTransferAsync(
        int id,
        DogTransferDecisionRequest request,
        bool approve)
    {
        var userId = GetCurrentUserId();
        var shelterId = await GetCurrentShelterIdAsync();
        if (string.IsNullOrWhiteSpace(userId) || shelterId is null)
        {
            return BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."));
        }

        try
        {
            return Ok(approve
                ? await dogTransferService.ApproveTransferAsync(id, shelterId.Value, userId, request)
                : await dogTransferService.RejectTransferAsync(id, shelterId.Value, userId, request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    private async Task<ActionResult<DogTransferDetailsDto>> RunTransferActionAsync(
        Func<string, int?, bool, Task<DogTransferDetailsDto>> action)
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
