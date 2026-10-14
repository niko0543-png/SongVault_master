using SongVault.Domain.Common;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Domain.Activity;

/// <summary>
/// Une modification faite par un membre, gardée pour le fil d'activité et les badges (ADR 0017).
/// Écrit dans la même transaction que la modification, jamais modifié ensuite,
/// sauf NotifiedAt, que pose l'envoi des notifications (#12, ADR 0018).
/// </summary>
public sealed class ActivityEvent
{
    private ActivityEvent() { } // réservé à EF Core

    /// <summary>Numéro attribué par la base, croissant : départage deux événements de la même date.</summary>
    public long Id { get; private set; }
    public Guid BandId { get; private set; }
    /// <summary>Identifiant Identity de l'auteur ; null une fois son compte purgé.</summary>
    public string? ActorId { get; private set; }
    public ActivityType Type { get; private set; }
    /// <summary>Morceau concerné. Sans clé étrangère : l'événement survit à la suppression du morceau.</summary>
    public Guid SongId { get; private set; }
    public Guid? VersionId { get; private set; }
    /// <summary>Fichier (FileAdded, FileRemoved) ou commentaire (CommentAdded, CommentReplied) concerné.</summary>
    public Guid? EntityId { get; private set; }
    public ActivityDetails Details { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    /// <summary>Quand les notifications de l'événement ont été préparées (#12) ; null tant qu'elles ne l'ont pas été.</summary>
    public DateTimeOffset? NotifiedAt { get; private set; }

    public static ActivityEvent Record(Guid bandId, string actorId, ActivityType type, Guid songId,
        ActivityDetails details, DateTimeOffset now, Guid? versionId = null, Guid? entityId = null)
    {
        if (bandId == Guid.Empty) throw new DomainException("Un événement appartient à un groupe.");
        if (songId == Guid.Empty) throw new DomainException("Un événement porte sur un morceau.");
        if (string.IsNullOrWhiteSpace(actorId)) throw new DomainException("Un événement a un auteur.");
        if (!Enum.IsDefined(type)) throw new DomainException($"Type d'événement inconnu : {type}.");
        ArgumentNullException.ThrowIfNull(details);

        var onSong = type is ActivityType.SongCreated or ActivityType.SongUpdated or ActivityType.SongDeleted;
        var onVersion = type is ActivityType.VersionAdded or ActivityType.VersionUpdated;
        if (!onSong && versionId is null)
            throw new DomainException($"L'événement {type} porte sur une version.");
        if (!onSong && !onVersion && entityId is null)
            throw new DomainException($"L'événement {type} porte sur un fichier ou un commentaire.");

        return new ActivityEvent
        {
            BandId = bandId,
            ActorId = actorId,
            Type = type,
            SongId = songId,
            VersionId = onSong ? null : versionId,
            EntityId = onSong || onVersion ? null : entityId,
            Details = details,
            CreatedAt = now,
        };
    }

    public static ActivityEvent ForSong(ActivityType type, Song song, string actorId, DateTimeOffset now)
        => Record(song.BandId, actorId, type, song.Id, new ActivityDetails(song.Title), now);

    public static ActivityEvent ForVersion(ActivityType type, Song song, SongVersion version, string actorId,
        DateTimeOffset now)
        => Record(song.BandId, actorId, type, song.Id, new ActivityDetails(song.Title, version.Number, version.Title),
            now, version.Id);

    public static ActivityEvent ForFile(ActivityType type, Song song, SongVersion version, SongFile file,
        string actorId, DateTimeOffset now)
        => Record(song.BandId, actorId, type, song.Id,
            new ActivityDetails(song.Title, version.Number, version.Title, file.OriginalFileName),
            now, version.Id, file.Id);
}