using SongVault.Domain.Common;

namespace SongVault.Domain.Audit;

/// <summary>
/// Une ligne du journal d'audit du groupe (#19, ADR 0026) : qui a fait quoi, et quand.
/// Écrite dans la même transaction que l'action, jamais modifiée ni supprimée ensuite : pas de méthode
/// qui la change, un contrôle dans SaveChangesAsync et un déclencheur SQL. Elle part seulement avec son groupe,
/// ou au bout d'un an.
/// </summary>
public sealed class AuditEntry
{
    public const int SubjectMaxLength = 300;
    public const int DetailsMaxLength = 200;
    public const int UserIdMaxLength = 450;      // même longueur que AspNetUsers.Id

    /// <summary>Durée de conservation : au-delà, la ligne est effacée (rétention, ADR 0026).</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(365);

    private AuditEntry() { } // réservé à EF Core

    /// <summary>Numéro attribué par la base, croissant : départage deux lignes de la même date.</summary>
    public long Id { get; private set; }
    public Guid BandId { get; private set; }
    /// <summary>Identifiant Identity de l'auteur ; null quand c'est SongVault (antivirus).</summary>
    public string? ActorId { get; private set; }
    public AuditAction Action { get; private set; }
    /// <summary>Ce qui est visé, tel qu'il s'appelait : titre, nom de fichier, libellé, adresse masquée.</summary>
    public string? Subject { get; private set; }
    /// <summary>Compte visé (membre retiré, rôle changé, auteur du commentaire supprimé), sans clé étrangère.</summary>
    public string? TargetUserId { get; private set; }
    /// <summary>Précision courte : ancien et nouveau rôle, ancien nom, menace détectée…</summary>
    public string? Details { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static AuditEntry Record(Guid bandId, string? actorId, AuditAction action, string? subject,
        DateTimeOffset now, string? targetUserId = null, string? details = null)
    {
        if (bandId == Guid.Empty) throw new DomainException("Une ligne du journal appartient à un groupe.");
        if (!Enum.IsDefined(action)) throw new DomainException($"Action inconnue : {action}.");
        if (actorId is not null && string.IsNullOrWhiteSpace(actorId))
            throw new DomainException("L'auteur d'une ligne du journal est un compte, ou personne (SongVault).");

        return new AuditEntry
        {
            BandId = bandId,
            ActorId = actorId,
            Action = action,
            // Tronqué, jamais refusé : le journal ne doit pas faire échouer l'action qu'il raconte
            Subject = Truncate(subject, SubjectMaxLength),
            TargetUserId = string.IsNullOrWhiteSpace(targetUserId) ? null : targetUserId,
            Details = Truncate(details, DetailsMaxLength),
            OccurredAt = now,
        };
    }

    /// <summary>« niko@exemple.fr » → « n***@exemple.fr » : assez pour reconnaître une invitation, pas pour la recopier.</summary>
    public static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }

    private static string? Truncate(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length <= maxLength ? trimmed : trimmed[..(maxLength - 1)] + "…";
    }
}