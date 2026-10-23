using System.Security.Claims;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Contracts.Auth;
using SongVault.Api.Localization;

namespace SongVault.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> users) : ControllerBase
{
    /// <summary>Supprime le cookie d'authentification.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    /// <summary>Utilisateur connecté (401 si anonyme), avec sa langue (#26). Le front l'appelle au démarrage.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me()
    {
        // La langue se lit en base : changée sur un autre appareil, elle n'est pas encore dans ce cookie
        var user = await users.GetUserAsync(User);
        return Ok(new CurrentUserResponse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            User.Identity?.Name ?? "",
            user is null ? null : await AccountLocale.GetAsync(users, user)));
    }
}