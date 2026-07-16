using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawConnect.Data;
using PawConnect.DTOs.Api;
using PawConnect.Services;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/adoption-copilot")]
[Produces("application/json")]
[Authorize(Roles = IdentitySeedData.AdopterRole)]
public sealed class AdoptionCopilotController(IAdoptionCopilotService adoptionCopilotService) : ControllerBase
{
    [HttpPost("search")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(AdoptionCopilotResponseApiDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdoptionCopilotResponseApiDto>> Search(
        [FromBody] AdoptionCopilotApiRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        try
        {
            var response = await adoptionCopilotService.AskAsync(userId, request.Message.Trim(), cancellationToken);
            var results = response.Results
                .Where(result => ApiDtoMapper.IsPublicSafeDog(result.Dog))
                .Select(result => new AdoptionCopilotResultApiDto(
                    ApiDtoMapper.ToDogListItem(result.Dog),
                    result.ScorePercent,
                    result.MatchLabel,
                    result.Reasons,
                    result.DisplayTags ?? [],
                    result.CautionTags ?? [],
                    result.SuggestedNextAction,
                    result.DistanceKm))
                .ToList();

            return Ok(new AdoptionCopilotResponseApiDto(
                response.AssistantMessage,
                results,
                (response.AppliedConstraints ?? [])
                    .Select(constraint => new AdoptionCopilotConstraintApiDto(constraint.Label, constraint.Value))
                    .ToList(),
                response.UsedAiEnhancement,
                response.UsedSemanticSearch,
                response.FallbackReason));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }
}
