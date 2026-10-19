using System.Text;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Me;
using SongVault.Api.Security;
using SongVault.Application.Accounts;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Accounts;
using SongVault.Domain.Common;

namespace SongVault.Api.Controllers;

/// <summary>
/// Sécurité du compte connecté (#19, ADR 0026) : double authentification par application (TOTP), codes de secours,
/// changement de mot de passe et historique de connexion. Remplace /api/auth/manage/2fa et /api/auth/manage/info
/// d'Identity, fermés dans Program.cs : ceux-là désactivent la double authentification sans redemander le mot de passe
/// et n'écrivent rien dans l'historique.
/// </summary>
[ApiController]
[Route("api/me")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class AccountSecurityController(
    UserManager<IdentityUser> users, SignInManager<IdentityUser> signInManager, AccountSecurityLog securityLog)
    : ControllerBase
{
    /// <summary>Codes de secours donnés à l'activation, et à chaque renouvellement.</summary>
    public const int RecoveryCodeCount = 10;
    private const string Issuer = "SongVault";

    /// <summary>La double authentification est-elle active, et combien de codes de secours restent-ils ?</summary>
    [HttpGet("two-factor")]
    [ProducesResponseType<TwoFactorStatusResponse>(StatusCodes.Status200OK)]
    public async Task<TwoFactorStatusResponse> Status()
    {
        var user = await CurrentUserAsync();
        var enabled = await users.GetTwoFactorEnabledAsync(user);
        return new TwoFactorStatusResponse(
            enabled,
            enabled ? await users.CountRecoveryCodesAsync(user) : 0,
            enabled && await signInManager.IsTwoFactorClientRememberedAsync(user));
    }

    /// <summary>Prépare une nouvelle clé pour l'application d'authentification. Rien n'est actif avant enable.</summary>
    /// <response code="200">La clé à saisir dans l'application, et son lien otpauth://.</response>
    /// <response code="409">La double authentification est déjà active.</response>
    [HttpPost("two-factor/setup")]
    [EnableRateLimiting(RateLimiting.Auth)]
    [ProducesResponseType<TwoFactorSetupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<TwoFactorSetupResponse> Setup()
    {
        var user = await CurrentUserAsync();
        if (await users.GetTwoFactorEnabledAsync(user))
            throw new ConflictException("La double authentification est déjà active : désactivez-la d'abord pour changer d'application.");

        await users.ResetAuthenticatorKeyAsync(user);             // change aussi l'empreinte de sécurité…
        await signInManager.RefreshSignInAsync(user);             // …donc cette session reçoit un cookie à jour
        var key = await users.GetAuthenticatorKeyAsync(user)
                  ?? throw new InvalidOperationException("Identity n'a pas créé de clé.");
        return new TwoFactorSetupResponse(FormatKey(key), AuthenticatorUri(user.Email ?? user.Id, key));
    }

    /// <summary>Active la double authentification avec un premier code de l'application ; renvoie les codes de secours.</summary>
    /// <response code="200">Activée. Les 10 codes de secours ne seront plus jamais montrés.</response>
    /// <response code="409">Déjà active, ou setup pas encore appelé.</response>
    /// <response code="422">Code incorrect.</response>
    [HttpPost("two-factor/enable")]
    [EnableRateLimiting(RateLimiting.Auth)]
    [ProducesResponseType<RecoveryCodesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<RecoveryCodesResponse> Enable(EnableTwoFactorRequest request, CancellationToken ct)
    {
        var user = await CurrentUserAsync();
        if (await users.GetTwoFactorEnabledAsync(user))
            throw new ConflictException("La double authentification est déjà active.");
        if (await users.GetAuthenticatorKeyAsync(user) is null)
            throw new ConflictException("Ajoutez d'abord SongVault à votre application d'authentification.");

        var code = request.Code!.Replace(" ", "").Replace("-", "");
        if (!await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, code))
            throw new DomainException("Code incorrect : vérifiez que l'heure du téléphone est juste, puis saisissez le code affiché maintenant.");

        await users.SetTwoFactorEnabledAsync(user, true);         // les autres sessions sont coupées dans la minute
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        await signInManager.RefreshSignInAsync(user);
        await securityLog.RecordAsync(user.Id, AccountEventType.TwoFactorEnabled, HttpContext.ClientInfo(), ct);
        return new RecoveryCodesResponse([.. codes ?? []]);
    }

    /// <summary>Désactive la double authentification. Le mot de passe est redemandé.</summary>
    /// <response code="204">Désactivée (ou déjà inactive) ; la clé de l'application ne sert plus.</response>
    /// <response code="422">Mot de passe incorrect, ou compte verrouillé après trop d'essais.</response>
    [HttpPost("two-factor/disable")]
    [EnableRateLimiting(RateLimiting.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Disable(PasswordConfirmationRequest request, CancellationToken ct)
    {
        var user = await CurrentUserAsync();
        await CheckPasswordAsync(user, request.Password!);
        if (!await users.GetTwoFactorEnabledAsync(user)) return NoContent();

        await users.SetTwoFactorEnabledAsync(user, false);
        await users.ResetAuthenticatorKeyAsync(user);             // l'ancienne entrée de l'application ne sert plus à rien
        await signInManager.ForgetTwoFactorClientAsync();
        await signInManager.RefreshSignInAsync(user);
        await securityLog.RecordAsync(user.Id, AccountEventType.TwoFactorDisabled, HttpContext.ClientInfo(), ct);
        return NoContent();
    }

    /// <summary>Remplace les codes de secours : les anciens ne marchent plus. Le mot de passe est redemandé.</summary>
    /// <response code="200">Les 10 nouveaux codes.</response>
    /// <response code="409">La double authentification n'est pas active.</response>
    /// <response code="422">Mot de passe incorrect, ou compte verrouillé après trop d'essais.</response>
    [HttpPost("two-factor/recovery-codes")]
    [EnableRateLimiting(RateLimiting.Auth)]
    [ProducesResponseType<RecoveryCodesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<RecoveryCodesResponse> RegenerateRecoveryCodes(PasswordConfirmationRequest request, CancellationToken ct)
    {
        var user = await CurrentUserAsync();
        await CheckPasswordAsync(user, request.Password!);
        if (!await users.GetTwoFactorEnabledAsync(user))
            throw new ConflictException("Activez d'abord la double authentification.");

        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        await signInManager.RefreshSignInAsync(user);
        await securityLog.RecordAsync(user.Id, AccountEventType.RecoveryCodesRegenerated, HttpContext.ClientInfo(), ct);
        return new RecoveryCodesResponse([.. codes ?? []]);
    }

    /// <summary>Ce navigateur redemandera le code à la prochaine connexion.</summary>
    [HttpPost("two-factor/forget-browser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgetBrowser()
    {
        await signInManager.ForgetTwoFactorClientAsync();
        return NoContent();
    }

    /// <summary>Change le mot de passe ; cette session reste ouverte, les autres sont coupées dans la minute.</summary>
    /// <response code="204">Mot de passe changé.</response>
    /// <response code="400">Mot de passe actuel incorrect (PasswordMismatch), ou nouveau mot de passe refusé par les règles.</response>
    [HttpPost("password")]
    [EnableRateLimiting(RateLimiting.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await CurrentUserAsync();
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword!, request.NewPassword!);
        if (!result.Succeeded)
        {
            // Mêmes clés que MapIdentityApi (PasswordMismatch, PasswordTooShort…) : le front les traduit déjà
            foreach (var error in result.Errors) ModelState.AddModelError(error.Code, error.Description);
            return ValidationProblem(ModelState);
        }

        await signInManager.RefreshSignInAsync(user);
        await securityLog.RecordAsync(user.Id, AccountEventType.PasswordChanged, HttpContext.ClientInfo(), ct);
        return NoContent();
    }

    /// <summary>Les 50 derniers événements de sécurité du compte (90 jours au plus), du plus récent au plus ancien.</summary>
    [HttpGet("account-events")]
    [ProducesResponseType<IReadOnlyList<AccountEventResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AccountEventResponse>> AccountEvents(
        [FromServices] ListAccountEventsHandler handler, CancellationToken ct)
        => [.. (await handler.HandleAsync(ct)).Select(AccountEventResponse.From)];

    private async Task<IdentityUser> CurrentUserAsync()
        => await users.GetUserAsync(User) ?? throw new NotFoundException("Account", "connecté");

    /// <summary>Compte les erreurs comme la connexion : 5 mots de passe faux verrouillent le compte quelques minutes.</summary>
    private async Task CheckPasswordAsync(IdentityUser user, string password)
    {
        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            throw new DomainException("Trop d'essais : votre compte est verrouillé quelques minutes.");
        if (!result.Succeeded)
            throw new DomainException("Mot de passe incorrect.");
    }

    /// <summary>« JBSWY3DPEHPK3PXP » → « JBSW Y3DP EHPK 3PXP », plus facile à recopier.</summary>
    internal static string FormatKey(string key)
    {
        var text = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            if (i > 0) text.Append(' ');
            text.Append(key.AsSpan(i, Math.Min(4, key.Length - i)));
        }
        return text.ToString();
    }

    internal static string AuthenticatorUri(string account, string key)
    {
        var issuer = UrlEncoder.Default.Encode(Issuer);
        return $"otpauth://totp/{issuer}:{UrlEncoder.Default.Encode(account)}?secret={key}&issuer={issuer}&digits=6";
    }
}