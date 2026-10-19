using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using SongVault.Application.Accounts;
using SongVault.Domain.Accounts;

namespace SongVault.Api.Auth;

/// <summary>
/// Le SignInManager d'Identity, qui écrit chaque tentative de connexion dans l'historique du compte (#19, ADR 0026).
/// /api/auth/login (MapIdentityApi) l'appelle deux fois quand la double authentification est active :
/// mot de passe seul (« RequiresTwoFactor », rien d'écrit), puis mot de passe et code (écrit ici).
/// Une adresse inconnue n'écrit rien : il n'y a pas de compte à qui la montrer.
/// </summary>
internal sealed class AuditingSignInManager(
    UserManager<IdentityUser> userManager, IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<IdentityUser> claimsFactory, IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<IdentityUser>> logger, IAuthenticationSchemeProvider schemes,
    IUserConfirmation<IdentityUser> confirmation, AccountSecurityLog securityLog)
    : SignInManager<IdentityUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task<SignInResult> PasswordSignInAsync(
        IdentityUser user, string password, bool isPersistent, bool lockoutOnFailure)
    {
        var result = await base.PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure);
        // Adresse pas confirmée (NotAllowed) ou code encore attendu (RequiresTwoFactor) : rien à raconter
        AccountEventType? type = result.Succeeded ? AccountEventType.SignedIn
            : result.IsLockedOut ? AccountEventType.LockedOut
            : result.RequiresTwoFactor || result.IsNotAllowed ? null
            : AccountEventType.SignInFailed;
        if (type is { } t) await RecordAsync(user.Id, t);
        return result;
    }

    public override async Task<SignInResult> TwoFactorAuthenticatorSignInAsync(string code, bool isPersistent, bool rememberClient)
    {
        var user = await GetTwoFactorAuthenticationUserAsync();
        var result = await base.TwoFactorAuthenticatorSignInAsync(code, isPersistent, rememberClient);
        if (user is not null) await RecordAsync(user.Id, CodeOutcome(result, AccountEventType.SignedInWithCode));
        return result;
    }

    public override async Task<SignInResult> TwoFactorRecoveryCodeSignInAsync(string recoveryCode)
    {
        var user = await GetTwoFactorAuthenticationUserAsync();
        var result = await base.TwoFactorRecoveryCodeSignInAsync(recoveryCode);
        if (user is not null) await RecordAsync(user.Id, CodeOutcome(result, AccountEventType.SignedInWithRecoveryCode));
        return result;
    }

    private static AccountEventType CodeOutcome(SignInResult result, AccountEventType success)
        => result.Succeeded ? success : result.IsLockedOut ? AccountEventType.LockedOut : AccountEventType.CodeRejected;

    private Task RecordAsync(string userId, AccountEventType type)
        => securityLog.RecordAsync(userId, type, Context.ClientInfo(), CancellationToken.None);
}

internal static class ClientInfoExtensions
{
    /// <summary>IP du client (corrigée par UseForwardedHeaders derrière nginx) et User-Agent, bruts.</summary>
    public static ClientInfo ClientInfo(this HttpContext context)
        => new(context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers.UserAgent.ToString());
}