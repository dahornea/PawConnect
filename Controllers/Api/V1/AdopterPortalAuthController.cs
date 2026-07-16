using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PawConnect.Data;
using PawConnect.DTOs.Api;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AdopterPortalAuthController(
    IAntiforgery antiforgery,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : ControllerBase
{
    [HttpGet("antiforgery")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AntiforgeryTokenApiDto), StatusCodes.Status200OK)]
    public ActionResult<AntiforgeryTokenApiDto> GetAntiforgeryToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new AntiforgeryTokenApiDto(
            tokens.RequestToken ?? string.Empty,
            tokens.HeaderName ?? "X-XSRF-TOKEN"));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(AdopterPortalUserApiDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdopterPortalUserApiDto>> Login(
        [FromBody] AdopterPortalLoginRequest request)
    {
        var email = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Unauthorized(new ApiErrorResponse("The email or password is incorrect."));
        }

        var passwordResult = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);
        if (!passwordResult.Succeeded)
        {
            return Unauthorized(new ApiErrorResponse("The email or password is incorrect."));
        }

        if (!await userManager.IsInRoleAsync(user, IdentitySeedData.AdopterRole))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new ApiErrorResponse("The React portal is available to adopter accounts only."));
        }

        await signInManager.SignInAsync(user, request.RememberMe);
        return Ok(await ToCurrentUserAsync(user));
    }

    [HttpGet("me")]
    [Authorize(Roles = IdentitySeedData.AdopterRole)]
    [ProducesResponseType(typeof(AdopterPortalUserApiDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdopterPortalUserApiDto>> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        return user is null ? Unauthorized() : Ok(await ToCurrentUserAsync(user));
    }

    [HttpPost("logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    private async Task<AdopterPortalUserApiDto> ToCurrentUserAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new AdopterPortalUserApiDto(
            user.Id,
            user.Email ?? string.Empty,
            string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? "Adopter" : user.FullName,
            roles.ToList());
    }
}
