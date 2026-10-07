using System.Security.Claims;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Contracts.Auth;

namespace SongVault.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(SignInManager<IdentityUser> signInManager) : ControllerBase
{
    /// <summary>Supprime le cookie d'authentification.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    /// <summary>Utilisateur connecté (401 si anonyme). Le front l'appelle au démarrage.</summary>
    [HttpGet("me")]
    public ActionResult<CurrentUserResponse> Me()
        => Ok(new CurrentUserResponse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            User.Identity?.Name ?? ""));
}