using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawConnect.Data;
using PawConnect.DTOs.Api;
using PawConnect.Services;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/volunteer-tasks")]
[Produces("application/json")]
[Authorize(Roles = $"{IdentitySeedData.AdminRole},{IdentitySeedData.ShelterRole},{IdentitySeedData.VolunteerRole}")]
public class VolunteerTasksController(
    IVolunteerTaskService volunteerTaskService,
    IShelterService shelterService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<VolunteerTaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<VolunteerTaskDto>>> GetTasks([FromQuery] VolunteerTaskFilterDto filter)
    {
        if (User.IsInRole(IdentitySeedData.AdminRole))
        {
            return Ok(await volunteerTaskService.GetAdminTasksAsync(filter));
        }

        if (User.IsInRole(IdentitySeedData.ShelterRole))
        {
            var shelterId = await GetCurrentShelterIdAsync();
            return shelterId is null
                ? BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."))
                : Ok(await volunteerTaskService.GetShelterTasksAsync(shelterId.Value, filter));
        }

        var userId = GetCurrentUserId();
        return string.IsNullOrWhiteSpace(userId)
            ? Unauthorized()
            : Ok(await volunteerTaskService.GetVolunteerTasksAsync(userId, filter));
    }

    [HttpGet("available")]
    [Authorize(Roles = IdentitySeedData.VolunteerRole)]
    [ProducesResponseType(typeof(IReadOnlyList<VolunteerTaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VolunteerTaskDto>>> GetAvailableTasks([FromQuery] VolunteerTaskFilterDto filter)
    {
        var userId = GetCurrentUserId();
        return string.IsNullOrWhiteSpace(userId)
            ? Unauthorized()
            : Ok(await volunteerTaskService.GetOpenTasksForVolunteerAsync(userId, filter));
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(VolunteerTaskStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VolunteerTaskStatsDto>> GetStats()
    {
        if (User.IsInRole(IdentitySeedData.AdminRole))
        {
            return Ok(await volunteerTaskService.GetTaskStatsAsync());
        }

        if (User.IsInRole(IdentitySeedData.ShelterRole))
        {
            var shelterId = await GetCurrentShelterIdAsync();
            return shelterId is null
                ? BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."))
                : Ok(await volunteerTaskService.GetTaskStatsAsync(shelterId.Value));
        }

        var userId = GetCurrentUserId();
        return string.IsNullOrWhiteSpace(userId)
            ? Unauthorized()
            : Ok(await volunteerTaskService.GetTaskStatsAsync(volunteerUserId: userId));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(VolunteerTaskDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VolunteerTaskDetailsDto>> GetTask(int id)
    {
        var isAdmin = User.IsInRole(IdentitySeedData.AdminRole);
        var shelterId = User.IsInRole(IdentitySeedData.ShelterRole) ? await GetCurrentShelterIdAsync() : null;
        var volunteerUserId = User.IsInRole(IdentitySeedData.VolunteerRole) ? GetCurrentUserId() : null;
        var task = await volunteerTaskService.GetTaskDetailsAsync(id, shelterId, volunteerUserId, isAdmin);
        return task is null
            ? NotFound(new ApiErrorResponse("Volunteer task was not found or is not visible to this account."))
            : Ok(task);
    }

    [HttpGet("volunteers")]
    [Authorize(Roles = $"{IdentitySeedData.AdminRole},{IdentitySeedData.ShelterRole}")]
    [ProducesResponseType(typeof(IReadOnlyList<VolunteerProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VolunteerProfileDto>>> GetVolunteers()
    {
        if (User.IsInRole(IdentitySeedData.AdminRole))
        {
            return Ok(await volunteerTaskService.GetAllVolunteersAsync());
        }

        var shelterId = await GetCurrentShelterIdAsync();
        return shelterId is null
            ? BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."))
            : Ok(await volunteerTaskService.GetVolunteersForShelterAsync(shelterId.Value));
    }

    [HttpPost]
    [Authorize(Roles = IdentitySeedData.ShelterRole)]
    [ProducesResponseType(typeof(VolunteerTaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VolunteerTaskDto>> CreateTask([FromBody] VolunteerTaskCreateRequest request)
    {
        var userId = GetCurrentUserId();
        var shelterId = await GetCurrentShelterIdAsync();
        if (string.IsNullOrWhiteSpace(userId) || shelterId is null)
        {
            return BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."));
        }

        try
        {
            var created = await volunteerTaskService.CreateTaskAsync(shelterId.Value, userId, request);
            return CreatedAtAction(nameof(GetTask), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = IdentitySeedData.ShelterRole)]
    [ProducesResponseType(typeof(VolunteerTaskDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VolunteerTaskDetailsDto>> UpdateTask(int id, [FromBody] VolunteerTaskUpdateRequest request)
    {
        var userId = GetCurrentUserId();
        var shelterId = await GetCurrentShelterIdAsync();
        if (string.IsNullOrWhiteSpace(userId) || shelterId is null)
        {
            return BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."));
        }

        try
        {
            return Ok(await volunteerTaskService.UpdateTaskAsync(id, shelterId.Value, userId, request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPatch("{id:int}/assign")]
    [Authorize(Roles = IdentitySeedData.ShelterRole)]
    [ProducesResponseType(typeof(VolunteerTaskDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VolunteerTaskDetailsDto>> AssignTask(int id, [FromBody] VolunteerTaskAssignRequest request)
    {
        var userId = GetCurrentUserId();
        var shelterId = await GetCurrentShelterIdAsync();
        if (string.IsNullOrWhiteSpace(userId) || shelterId is null)
        {
            return BadRequest(new ApiErrorResponse("Current shelter profile could not be resolved."));
        }

        try
        {
            return Ok(await volunteerTaskService.AssignTaskAsync(id, shelterId.Value, userId, request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPatch("{id:int}/accept")]
    [Authorize(Roles = IdentitySeedData.VolunteerRole)]
    [ProducesResponseType(typeof(VolunteerTaskDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<VolunteerTaskDetailsDto>> AcceptTask(int id, [FromBody] VolunteerTaskActionRequest? request) =>
        RunVolunteerActionAsync(userId => volunteerTaskService.AcceptTaskAsync(id, userId, request));

    [HttpPatch("{id:int}/start")]
    [Authorize(Roles = IdentitySeedData.VolunteerRole)]
    [ProducesResponseType(typeof(VolunteerTaskDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<VolunteerTaskDetailsDto>> StartTask(int id, [FromBody] VolunteerTaskActionRequest? request) =>
        RunVolunteerActionAsync(userId => volunteerTaskService.StartTaskAsync(id, userId, request));

    [HttpPatch("{id:int}/complete")]
    [Authorize(Roles = IdentitySeedData.VolunteerRole)]
    [ProducesResponseType(typeof(VolunteerTaskDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<VolunteerTaskDetailsDto>> CompleteTask(int id, [FromBody] VolunteerTaskActionRequest? request) =>
        RunVolunteerActionAsync(userId => volunteerTaskService.CompleteTaskAsync(id, userId, request));

    [HttpPatch("{id:int}/cancel")]
    [Authorize(Roles = $"{IdentitySeedData.AdminRole},{IdentitySeedData.ShelterRole}")]
    [ProducesResponseType(typeof(VolunteerTaskDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VolunteerTaskDetailsDto>> CancelTask(int id, [FromBody] VolunteerTaskActionRequest? request)
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
            return Ok(await volunteerTaskService.CancelTaskAsync(id, shelterId, userId, isAdmin, request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("{id:int}/comments")]
    [ProducesResponseType(typeof(VolunteerTaskDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VolunteerTaskDetailsDto>> AddComment(int id, [FromBody] VolunteerTaskActionRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var isAdmin = User.IsInRole(IdentitySeedData.AdminRole);
        var shelterId = User.IsInRole(IdentitySeedData.ShelterRole) ? await GetCurrentShelterIdAsync() : null;

        try
        {
            return Ok(await volunteerTaskService.AddTaskCommentAsync(id, userId, request, shelterId, isAdmin));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    private async Task<ActionResult<VolunteerTaskDetailsDto>> RunVolunteerActionAsync(
        Func<string, Task<VolunteerTaskDetailsDto>> action)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await action(userId));
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
