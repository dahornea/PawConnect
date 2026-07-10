using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawConnect.Data;
using PawConnect.Services;
using PawConnect.Services.Simulation;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/simulations")]
[Produces("application/json")]
[Authorize(Roles = "Admin,Shelter")]
[Tags("Scenario Simulator")]
public sealed class SimulationsController(
    IShelterSimulationService simulationService,
    IShelterService shelterService) : ControllerBase
{
    [HttpGet("templates")]
    [ProducesResponseType(typeof(IReadOnlyList<SimulationTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SimulationTemplateDto>>> GetTemplates(CancellationToken cancellationToken) =>
        Ok(await simulationService.GetTemplatesAsync(cancellationToken));

    [HttpGet("scenarios")]
    [ProducesResponseType(typeof(IReadOnlyList<SimulationScenarioListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SimulationScenarioListItemDto>>> GetScenarios(CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync();
        return access is null ? Forbid() : Ok(await simulationService.GetScenariosAsync(access, cancellationToken));
    }

    [HttpGet("scenarios/{id:int}")]
    [ProducesResponseType(typeof(SimulationScenarioListItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationScenarioListItemDto>> GetScenario(int id, CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync();
        if (access is null) return Forbid();
        var scenario = await simulationService.GetScenarioAsync(id, access, cancellationToken);
        return scenario is null ? NotFound() : Ok(scenario);
    }

    [HttpPost("run")]
    [ProducesResponseType(typeof(SimulationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SimulationResultDto>> Run([FromBody] SimulationRunRequestDto request, CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync();
        if (access is null) return Forbid();
        try { return Ok(await simulationService.RunAsync(request, access, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("save-and-run")]
    [ProducesResponseType(typeof(SimulationSavedRunDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SimulationSavedRunDto>> SaveAndRun([FromBody] SimulationSaveRequestDto request, CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync();
        if (access is null) return Forbid();
        try { return Ok(await simulationService.SaveAndRunAsync(request, access, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("scenarios/{id:int}/rerun")]
    public async Task<ActionResult<SimulationSavedRunDto>> Rerun(int id, CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync();
        if (access is null) return Forbid();
        try { return Ok(await simulationService.RerunAsync(id, access, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPatch("scenarios/{id:int}")]
    public async Task<IActionResult> UpdateScenario(int id, [FromBody] UpdateSimulationScenarioRequest request, CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync();
        if (access is null) return Forbid();
        try
        {
            if (!string.IsNullOrWhiteSpace(request.Name)) await simulationService.RenameAsync(id, request.Name, access, cancellationToken);
            if (request.IsPinned.HasValue) await simulationService.SetPinnedAsync(id, request.IsPinned.Value, access, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("compare")]
    [ProducesResponseType(typeof(SimulationComparisonDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SimulationComparisonDto>> Compare([FromQuery] int firstScenarioId, [FromQuery] int secondScenarioId, CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync();
        if (access is null) return Forbid();
        try { return Ok(await simulationService.CompareAsync(firstScenarioId, secondScenarioId, access, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpDelete("scenarios/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync();
        if (access is null) return Forbid();
        try { await simulationService.DeleteAsync(id, access, cancellationToken); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    private async Task<SimulationAccessContext?> GetAccessAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return null;
        if (User.IsInRole(IdentitySeedData.AdminRole)) return new SimulationAccessContext(userId, true);
        if (!User.IsInRole(IdentitySeedData.ShelterRole)) return null;
        var shelter = await shelterService.GetShelterForUserAsync(userId);
        return shelter is null ? null : new SimulationAccessContext(userId, false, shelter.Id);
    }
}

public sealed record UpdateSimulationScenarioRequest(string? Name, bool? IsPinned);
