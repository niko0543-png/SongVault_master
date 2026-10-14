using SongVault.Domain.Activity;
using SongVault.Domain.Notifications;

namespace SongVault.Application.Notifications;

/// <summary>Un événement du fil, avec ce qu'il faut pour choisir les destinataires et écrire l'e-mail.</summary>
/// <param name="ActorEmail">null : compte de l'auteur supprimé.</param>
/// <param name="CommentBody">Texte du commentaire (CommentAdded, CommentReplied) ; null s'il a été supprimé entre-temps.</param>
public sealed record NotificationEvent(
    long Id, Guid BandId, string BandName, ActivityType Type, string? ActorId, string? ActorEmail,
    Guid SongId, Guid? VersionId, string SongTitle, int? VersionNumber, string? VersionTitle, string? FileName,
    string? CommentBody, DateTimeOffset CreatedAt);

/// <summary>Un membre qui peut recevoir un e-mail.</summary>
public sealed record NotificationRecipient(string UserId, string Email);

/// <summary>Un e-mail à prévoir : pour qui, pour quel type, tout de suite ou au résumé.</summary>
public sealed record PlannedNotification(NotificationRecipient To, NotificationKind Kind, NotificationMode Mode);

/// <summary>Un élément de résumé et son événement ; IsMember false si le compte a quitté le groupe depuis.</summary>
public sealed record DigestEntry(long ItemId, NotificationKind Kind, NotificationEvent Event, bool IsMember);

/// <summary>Contenu d'une tâche « notification.send ».</summary>
public sealed record SendNotificationPayload(long EventId, string UserId, NotificationKind Kind);