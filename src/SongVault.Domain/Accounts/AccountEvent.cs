using SongVault.Domain.Common;

namespace SongVault.Domain.Accounts;

/// <summary>Ce qui arrive à la sécurité d'un compte (#19). Enregistré en texte dans AccountEvents.Type.</summary>
public enum AccountEventType
{
    /// <summary>Connexion avec le mot de passe seul (pas de double authentification, ou navigateur reconnu).</summary>
    SignedIn,
    SignedInWithCode,
    SignedInWithRecoveryCode,
    /// <summary>Mot de passe incorrect.</summary>
    SignInFailed,
    /// <summary>Bon mot de passe, mauvais code de l'application ou code de secours.</summary>
    CodeRejected,
    LockedOut,
    PasswordChanged,
    TwoFactorEnabled,
    TwoFactorDisabled,
    RecoveryCodesRegenerated,
}

/// <summary>
/// Une ligne de l'historique de connexion d'un compte (#19, ADR 0026), visible par lui seul dans « Mon compte ».
/// Ni adresse IP complète ni navigateur exact : le réseau (/24 ou /48) et « Firefox sur Windows » suffisent
/// à reconnaître une connexion qui n'est pas la sienne. Gardée 90 jours.
/// </summary>
public sealed class AccountEvent
{
    public const int UserIdMaxLength = 450;      // même longueur que AspNetUsers.Id
    public const int NetworkMaxLength = 50;
    public const int DeviceMaxLength = 100;

    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    private AccountEvent() { } // réservé à EF Core

    public long Id { get; private set; }
    public string UserId { get; private set; } = default!;
    public AccountEventType Type { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    /// <summary>Réseau d'origine, IP masquée (« 203.0.113.0/24 ») ; null si inconnue.</summary>
    public string? Network { get; private set; }
    /// <summary>Navigateur et système, en clair (« Firefox sur Windows ») ; null si inconnus.</summary>
    public string? Device { get; private set; }

    public static AccountEvent Record(string userId, AccountEventType type, string? network, string? device,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("Une ligne de l'historique appartient à un compte.");
        if (!Enum.IsDefined(type)) throw new DomainException($"Événement de compte inconnu : {type}.");

        return new AccountEvent
        {
            UserId = userId,
            Type = type,
            OccurredAt = now,
            Network = Cut(network, NetworkMaxLength),
            Device = Cut(device, DeviceMaxLength),
        };
    }

    private static string? Cut(string? value, int maxLength)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, maxLength)];
}