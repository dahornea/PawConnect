using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Services.Intelligence;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/intelligence")]
[Produces("application/json")]
[Authorize(Roles = "Admin,Shelter,Adopter")]
public sealed class IntelligenceController(
    IIntelligenceInsightService insightService,
    IShelterService shelterService) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(IntelligenceSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IntelligenceSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var scope = await GetScopeAsync();
        return scope is null ? Forbid() : Ok(await insightService.GetSummaryAsync(scope, cancellationToken));
    }

    [HttpGet("insights")]
    [ProducesResponseType(typeof(PagedResult<OperationalInsightDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OperationalInsightDto>>> GetInsights(
        [FromQuery] IntelligenceSeverity? severity,
        [FromQuery] IntelligenceCategory? category,
        [FromQuery] IntelligenceInsightStatus? status,
        [FromQuery] string? entityType,
        [FromQuery] string? search,
        [FromQuery] bool includeSnoozed = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync();
        if (scope is null) return Forbid();
        return Ok(await insightService.GetInsightsAsync(scope, new IntelligenceInsightQuery(severity, category, status, entityType, search, includeSnoozed, page, pageSize), cancellationToken));
    }

    [HttpGet("insights/{id:int}")]
    [ProducesResponseType(typeof(OperationalInsightDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationalInsightDto>> GetInsight(int id, CancellationToken cancellationToken)
    {
        var scope = await GetScopeAsync();
        if (scope is null) return Forbid();
        var insight = await insightService.GetInsightDetailsAsync(id, scope, cancellationToken);
        return insight is null ? NotFound() : Ok(insight);
    }

    [HttpPatch("insights/{id:int}/acknowledge")]
    public async Task<IActionResult> Acknowledge(int id, CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync();
        if (access.Scope is null || access.UserId is null) return Forbid();
        try
        {
            await insightService.AcknowledgeAsync(id, access.Scope, access.UserId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("insights/{id:int}/snooze")]
    public async Task<IActionResult> Snooze(int id, [FromBody] SnoozeInsightRequest request, CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync();
        if (access.Scope is null || access.UserId is null) return Forbid();
        try
        {
            await insightService.SnoozeAsync(id, access.Scope, access.UserId, TimeSpan.FromHours(request.Hours), cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("insights/{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id, [FromBody] ResolveInsightRequest request, CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync();
        if (access.Scope is null || access.UserId is null) return Forbid();
        try
        {
            await insightService.ResolveAsync(id, access.Scope, access.UserId, request.Reason, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("insights/{id:int}/reopen")]
    public async Task<IActionResult> Reopen(int id, CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync();
        if (access.Scope is null || access.UserId is null) return Forbid();
        try
        {
            await insightService.ReopenAsync(id, access.Scope, access.UserId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(IntelligenceEvaluationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<IntelligenceEvaluationResult>> Refresh(CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync();
        if (access.Scope is null || access.UserId is null) return Forbid();
        try
        {
            return Ok(await insightService.RefreshAsync(access.Scope, access.UserId, cancellationToken));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("recently", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message });
        }
    }

    [HttpGet("/api/v1/shelter/intelligence")]
    [Authorize(Roles = IdentitySeedData.ShelterRole)]
    public Task<ActionResult<PagedResult<OperationalInsightDto>>> GetShelterIntelligence(CancellationToken cancellationToken)
        => GetInsights(null, null, null, null, null, false, 1, 25, cancellationToken);

    [HttpGet("/api/v1/admin/intelligence")]
    [Authorize(Roles = IdentitySeedData.AdminRole)]
    public Task<ActionResult<PagedResult<OperationalInsightDto>>> GetAdminIntelligence(CancellationToken cancellationToken)
        => GetInsights(null, null, null, null, null, false, 1, 25, cancellationToken);

    [HttpGet("/api/v1/adopter/insights")]
    [Authorize(Roles = IdentitySeedData.AdopterRole)]
    public Task<ActionResult<PagedResult<OperationalInsightDto>>> GetAdopterInsights(CancellationToken cancellationToken)
        => GetInsights(null, null, null, null, null, false, 1, 25, cancellationToken);

    private async Task<IntelligenceScope?> GetScopeAsync() => (await GetAccessAsync()).Scope;

    private async Task<(IntelligenceScope? Scope, string? UserId)> GetAccessAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return (null, null);
        if (User.IsInRole(IdentitySeedData.AdminRole)) return (new IntelligenceScope(IntelligenceAudienceType.Admin), userId);
        if (User.IsInRole(IdentitySeedData.ShelterRole))
        {
            var shelter = await shelterService.GetShelterForUserAsync(userId);
            return shelter is null ? (null, userId) : (new IntelligenceScope(IntelligenceAudienceType.Shelter, ShelterId: shelter.Id), userId);
        }
        return User.IsInRole(IdentitySeedData.AdopterRole)
            ? (new IntelligenceScope(IntelligenceAudienceType.Adopter, UserId: userId), userId)
            : (null, userId);
    }
}

public sealed record SnoozeInsightRequest(double Hours);
public sealed record ResolveInsightRequest(string Reason);
