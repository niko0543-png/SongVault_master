using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace SongVault.Api.Auth;

/// <summary>Durées des liens du compte (ADR 0010).</summary>
public static class AccountTokens
{
    public const string EmailConfirmationProvider = "EmailConfirmation";
    public static readonly TimeSpan EmailConfirmationLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromHours(2);
}

/// <summary>Options du jeton de confirmation : sa durée ne suit pas celle de la réinitialisation.</summary>
public sealed class EmailConfirmationTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public EmailConfirmationTokenProviderOptions()
    {
        Name = AccountTokens.EmailConfirmationProvider;            // « purpose » distinct : un code de l'un ne vaut rien pour l'autre
        TokenLifespan = AccountTokens.EmailConfirmationLifetime;
    }
}

/// <summary>Même mécanisme que le fournisseur par défaut d'Identity, avec ses propres options.</summary>
public sealed class EmailConfirmationTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<EmailConfirmationTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<IdentityUser>> logger)
    : DataProtectorTokenProvider<IdentityUser>(dataProtectionProvider, options, logger);