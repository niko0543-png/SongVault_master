using SongVault.Application.Abstractions;
using SongVault.Application.Files;
using SongVault.Domain.Activity;
using SongVault.Domain.Comments;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Application.Activity;

/// <summary>
/// Écrit les événements du fil d'activité (ADR 0017), au nom de l'utilisateur de la requête.
/// N'enregistre rien : l'événement part au SaveChangesAsync du handler, avec la modification,
/// dans la même transaction. Une modification refusée ne laisse donc aucun événement.
/// </summary>
public sealed class ActivityLog(IActivityRepository events, ICurrentUser user)
{
    public void SongChanged(ActivityType type, Song song, DateTimeOffset now)
        => events.Add(ActivityEvent.ForSong(type, song, user.UserId, now));

    public void VersionChanged(ActivityType type, Song song, SongVersion version, DateTimeOffset now)
        => events.Add(ActivityEvent.ForVersion(type, song, version, user.UserId, now));

    public void FileChanged(ActivityType type, Song song, SongVersion version, SongFile file, DateTimeOffset now)
        => events.Add(ActivityEvent.ForFile(type, song, version, file, user.UserId, now));

    public void Commented(SongFileLabel file, Comment comment, DateTimeOffset now)
        => events.Add(ActivityEvent.Record(comment.BandId, user.UserId,
            comment.IsReply ? ActivityType.CommentReplied : ActivityType.CommentAdded, file.SongId,
            new ActivityDetails(file.SongTitle, file.VersionNumber, file.VersionTitle, file.FileName),
            now, file.VersionId, comment.Id));
}