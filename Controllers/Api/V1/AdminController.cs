using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.DTOs.Api;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/admin")]
[Produces("application/json")]
[Authorize(Roles = IdentitySeedData.AdminRole)]
public class AdminController(
    ApplicationDbContext context,
    INotificationOutboxService notificationOutboxService,
    IAnalyticsService analyticsService) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(AdminPlatformSummaryApiDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminPlatformSummaryApiDto>> GetSummary(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var outboxSummary = await notificationOutboxService.GetAdminSummaryAsync(
            new NotificationOutboxFilter(),
            userId,
            cancellationToken);

        var summary = new AdminPlatformSummaryApiDto(
            DateTime.UtcNow,
            await context.Shelters.CountAsync(cancellationToken),
            await context.Dogs.CountAsync(
                dog => dog.Status == DogStatus.Available || dog.Status == DogStatus.Reserved,
                cancellationToken),
            await context.Dogs.CountAsync(dog => dog.Status == DogStatus.Adopted, cancellationToken),
            await context.Dogs.CountAsync(dog => dog.Status == DogStatus.InTreatment, cancellationToken),
            await context.AdoptionRequests.CountAsync(
                request => request.Status == AdoptionRequestStatus.Pending,
                cancellationToken),
            ApiDtoMapper.ToNotificationOutboxSummary(outboxSummary));

        return Ok(summary);
    }

    [HttpGet("analytics")]
    [ProducesResponseType(typeof(AdminAnalyticsSummaryApiDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminAnalyticsSummaryApiDto>> GetAnalytics(
        [FromQuery] int days = 30,
        [FromQuery] int? shelterId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var range = AnalyticsDateRange.LastDays(Math.Clamp(days, 1, 366));
            var analytics = await analyticsService.GetAdminAnalyticsAsync(
                range,
                shelterId,
                userId,
                cancellationToken);

            return Ok(ApiDtoMapper.ToAdminAnalyticsSummary(analytics));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
