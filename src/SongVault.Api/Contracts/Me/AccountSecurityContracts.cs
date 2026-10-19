using System.ComponentModel.DataAnnotations;

using SongVault.Application.Accounts;
using SongVault.Domain.Accounts;

namespace SongVault.Api.Contracts.Me;

/// <summary>
/// État de la double authentification. RecoveryCodesLeft : codes de secours encore utilisables.
/// BrowserRemembered : ce navigateur ne redemande pas de code (14 jours après une connexion avec code).
/// </summary>
public sealed record TwoFactorStatusResponse(bool Enabled, int RecoveryCodesLeft, bool BrowserRemembered);

/// <summary>
/// SharedKey : la clé à saisir dans l'application d'authentification, par groupes de 4 caractères.
/// AuthenticatorUri : le lien otpauth:// que l'application ouvre directement sur téléphone.
/// </summary>
public sealed record TwoFactorSetupResponse(string SharedKey, string AuthenticatorUri);

/// <summary>Codes de secours : montrés une seule fois, chacun ne sert qu'une fois.</summary>
public sealed record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);

/// <summary>Code à 6 chiffres de l'application ; espaces et tirets ignorés.</summary>
public sealed record EnableTwoFactorRequest([Required, MaxLength(20)] string? Code);

/// <summary>Mot de passe redemandé avant de baisser la sécurité du compte.</summary>
public sealed record PasswordConfirmationRequest([Required] string? Password);

public sealed record ChangePasswordRequest([Required] string? CurrentPassword, [Required] string? NewPassword);

public sealed record AccountEventResponse(long Id, AccountEventType Type, DateTimeOffset OccurredAt, string? Network, string? Device)
{
    public static AccountEventResponse From(AccountEventDto d) => new(d.Id, d.Type, d.OccurredAt, d.Network, d.Device);
}