using System.Security.Cryptography;

using SongVault.Domain.Common;

namespace SongVault.Domain.Sharing;

/// <summary>
/// Lien d'écoute seule vers une version ou une sortie, ouvert sans compte (ADR 0019).
/// Seule l'empreinte SHA-256 du jeton est gardée ; le mot de passe, s'il y en a un, est haché.
/// </summary>
public sealed class ShareLink
{
    public const int LabelMaxLength = 100;
    public const int TokenHashLength = 32;          // SHA-256
    public const int AccessSaltLength = 16;
    public const int PasswordMinLength = 6;
    public const int PasswordMaxLength = 128;
    public const int MinLifetimeDays = 1;
    public const int MaxLifetimeDays = 90;
    public const int DefaultLifetimeDays = 30;

    private ShareLink() { } // réservé à EF Core

    public Guid Id { get; private set; }
    public Guid BandId { get; private set; }
    public ShareTargetType TargetType { get; private set; }
    /// <summary>Renseigné pour TargetType = Version, null sinon.</summary>
    public Guid? VersionId { get; private set; }
    /// <summary>Renseigné pour TargetType = Release, null sinon.</summary>
    public Guid? ReleaseId { get; private set; }
    /// <summary>Pour qui ou pourquoi (« Studio Delta, mix »), vu des membres seulement.</summary>
    public string? Label { get; private set; }
    public byte[] TokenHash { get; private set; } = default!;
    public string? PasswordHash { get; private set; }
    /// <summary>Sel aléatoire du lien : l'empreinte d'adresse IP du journal ne se compare pas d'un lien à l'autre.</summary>
    public byte[] AccessSalt { get; private set; } = default!;
    /// <summary>null quand le compte qui l'a créé a été supprimé : le lien appartient au groupe.</summary>
    public string? CreatedById { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool HasPassword => PasswordHash is not null;

    public static ShareLink ForVersion(Guid bandId, Guid versionId, string? label, int lifetimeDays,
        byte[] tokenHash, string? passwordHash, string createdById, DateTimeOffset now)
        => Create(bandId, ShareTargetType.Version, versionId, null, label, lifetimeDays, tokenHash, passwordHash, createdById, now);

    public static ShareLink ForRelease(Guid bandId, Guid releaseId, string? label, int lifetimeDays,
        byte[] tokenHash, string? passwordHash, string createdById, DateTimeOffset now)
        => Create(bandId, ShareTargetType.Release, null, releaseId, label, lifetimeDays, tokenHash, passwordHash, createdById, now);

    private static ShareLink Create(Guid bandId, ShareTargetType type, Guid? versionId, Guid? releaseId, string? label,
        int lifetimeDays, byte[] tokenHash, string? passwordHash, string createdById, DateTimeOffset now)
    {
        if (bandId == Guid.Empty || versionId == Guid.Empty || releaseId == Guid.Empty)
            throw new DomainException("Un lien de partage vise une version ou une sortie d'un groupe.");
        if (lifetimeDays is < MinLifetimeDays or > MaxLifetimeDays)
            throw new DomainException($"Un lien de partage dure de {MinLifetimeDays} à {MaxLifetimeDays} jours.");
        if (tokenHash is not { Length: TokenHashLength })
            throw new ArgumentException("Empreinte SHA-256 de 32 octets attendue.", nameof(tokenHash));
        if (passwordHash is not null && string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Mot de passe haché vide.", nameof(passwordHash));

        return new ShareLink
        {
            Id = Guid.CreateVersion7(now),
            BandId = bandId,
            TargetType = type,
            VersionId = versionId,
            ReleaseId = releaseId,
            Label = Text.Optional(label, LabelMaxLength, "libellé"),
            TokenHash = tokenHash,
            PasswordHash = passwordHash,
            AccessSalt = RandomNumberGenerator.GetBytes(AccessSaltLength),
            CreatedById = createdById,
            CreatedAt = now,
            ExpiresAt = now.AddDays(lifetimeDays),
        };
    }

    public ShareLinkStatus StatusAt(DateTimeOffset now) => this switch
    {
        { RevokedAt: not null } => ShareLinkStatus.Revoked,
        _ when now >= ExpiresAt => ShareLinkStatus.Expired,
        _ => ShareLinkStatus.Active,
    };

    public bool IsActiveAt(DateTimeOffset now) => StatusAt(now) == ShareLinkStatus.Active;

    /// <summary>Coupe le lien tout de suite. Déjà révoqué : rien ne change (on garde la première date).</summary>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}