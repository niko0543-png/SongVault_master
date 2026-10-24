using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

using SongVault.Api.Contracts.Auth;
using SongVault.Api.Localization;
using SongVault.Api.Security;

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

    /// <summary>
    /// Vérifie un lien de réinitialisation sans rien changer : la page l'appelle à l'ouverture du lien,
    /// pour dire tout de suite qu'il a expiré ou déjà servi (un code utilisé change le security stamp).
    /// Même réponse pour une adresse inconnue que pour un code faux, comme /api/auth/resetPassword.
    /// </summary>
    /// <response code="204">Lien valable : le formulaire peut s'afficher.</response>
    /// <response code="400">Lien expiré, déjà utilisé ou modifié (clé InvalidToken).</response>
    [HttpPost("checkResetCode"), AllowAnonymous]
    [EnableRateLimiting(RateLimiting.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckResetCode(CheckResetCodeRequest request)
    {
        var user = string.IsNullOrEmpty(request.Email) ? null : await users.FindByEmailAsync(request.Email);
        if (user is not null && await users.IsEmailConfirmedAsync(user) && Decode(request.ResetCode) is { } code
            && await users.VerifyUserTokenAsync(user, users.Options.Tokens.PasswordResetTokenProvider,
                                                UserManager<IdentityUser>.ResetPasswordTokenPurpose, code))
            return NoContent();

        // Même clé et même texte qu'Identity : le front les traduit déjà
        var invalid = users.ErrorDescriber.InvalidToken();
        ModelState.AddModelError(invalid.Code, invalid.Description);
        return ValidationProblem(ModelState);
    }

    /// <summary>Le code de l'e-mail est en Base64Url, comme celui qu'attend /api/auth/resetPassword.</summary>
    private static string? Decode(string? resetCode)
    {
        if (string.IsNullOrEmpty(resetCode)) return null;
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(resetCode)); }
        catch (FormatException) { return null; }
    }
}
