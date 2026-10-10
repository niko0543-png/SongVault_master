using SongVault.Domain.Bands;
using SongVault.Domain.Common;

namespace SongVault.Domain.Invitations;

public sealed class Invitation
{
    public const int EmailMaxLength = 256;     // même longueur que AspNetUsers.Email
    public const int TokenHashLength = 32;     // SHA-256
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private Invitation() { } // réservé à EF Core

    public Guid Id { get; private set; }
    public Guid BandId { get; private set; }
    public string Email { get; private set; } = default!;
    public BandRole Role { get; private set; }
    public byte[] TokenHash { get; private set; } = default!;
    public string InvitedById { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public string? AcceptedById { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Nouvelle invitation, valable <see cref="Lifetime"/>. On n'invite jamais directement un Owner.</summary>
    public static Invitation Create(
        Guid bandId, string email, BandRole role, byte[] tokenHash, string invitedById, DateTimeOffset now)
    {
        if (role == BandRole.Owner)
            throw new DomainException("On invite un Member ou un Guest ; un Owner se nomme ensuite depuis la page Membres.");
        if (tokenHash is not { Length: TokenHashLength })
            throw new ArgumentException("Empreinte SHA-256 de 32 octets attendue.", nameof(tokenHash));

        return new Invitation
        {
            Id = Guid.CreateVersion7(now),
            BandId = bandId,
            Email = NormalizeEmail(email),
            Role = role,
            TokenHash = tokenHash,
            InvitedById = invitedById,
            CreatedAt = now,
            ExpiresAt = now + Lifetime,
        };
    }

    /// <summary>Forme comparable d'une adresse : sans espaces autour, en minuscules. Contrôle minimal du format.</summary>
    public static string NormalizeEmail(string email)
    {
        var clean = Text.Required(email, EmailMaxLength, "e-mail");
        var at = clean.IndexOf('@');
        if (at <= 0 || at != clean.LastIndexOf('@') || at == clean.Length - 1)
            throw new DomainException("Adresse e-mail invalide.");
        return clean.ToLowerInvariant();
    }

    public InvitationStatus StatusAt(DateTimeOffset now) => this switch
    {
        { AcceptedAt: not null } => InvitationStatus.Accepted,
        { RevokedAt: not null } => InvitationStatus.Revoked,
        _ when now >= ExpiresAt => InvitationStatus.Expired,
        _ => InvitationStatus.Pending,
    };

    /// <summary>Lève InvitationUnavailableException (410) si l'invitation n'est plus en attente.</summary>
    public void EnsurePending(DateTimeOffset now)
    {
        var status = StatusAt(now);
        if (status != InvitationStatus.Pending) throw new InvitationUnavailableException(status);
    }

    /// <summary>Vrai si l'adresse du compte est celle invitée (sans tenir compte de la casse).</summary>
    public bool IsFor(string? email)
        => email is not null && string.Equals(email.Trim(), Email, StringComparison.OrdinalIgnoreCase);

    public void Accept(string userId, DateTimeOffset now)
    {
        EnsurePending(now);
        (AcceptedAt, AcceptedById) = (now, userId);
    }

    public void Revoke(DateTimeOffset now)
    {
        EnsurePending(now);
        RevokedAt = now;
    }
}