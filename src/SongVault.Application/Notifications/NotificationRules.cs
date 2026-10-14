using SongVault.Domain.Activity;
using SongVault.Domain.Comments;
using SongVault.Domain.Notifications;

namespace SongVault.Application.Notifications;

/// <summary>Qui reçoit quoi pour un événement (ADR 0018). Sans accès à la base : testé en unitaire.</summary>
public static class NotificationRules
{
    /// <summary>
    /// E-mails à prévoir pour cet événement. Jamais pour son auteur. Une version ajoutée prévient tous les membres ;
    /// un commentaire prévient les membres mentionnés d'une mention et les autres d'un commentaire.
    /// Une mention désactivée retombe sur la préférence des commentaires. Les modes Off sont retirés.
    /// </summary>
    public static IReadOnlyList<PlannedNotification> Plan(
        NotificationEvent e, IReadOnlyList<NotificationRecipient> members,
        Func<string, NotificationKind, NotificationMode> modeOf)
    {
        var audience = members.Where(m => m.UserId != e.ActorId);
        List<PlannedNotification> planned = [];

        switch (e.Type)
        {
            case ActivityType.VersionAdded:
                foreach (var member in audience)
                    planned.Add(new(member, NotificationKind.NewVersion, modeOf(member.UserId, NotificationKind.NewVersion)));
                break;

            case ActivityType.CommentAdded or ActivityType.CommentReplied when e.CommentBody is not null:
                var mentioned = CommentMentions.FindEmails(e.CommentBody);
                foreach (var member in audience)
                {
                    var mode = NotificationMode.Off;
                    var kind = NotificationKind.Comment;
                    if (mentioned.Contains(member.Email.ToLowerInvariant()))
                    {
                        kind = NotificationKind.Mention;
                        mode = modeOf(member.UserId, NotificationKind.Mention);
                    }
                    if (mode == NotificationMode.Off)
                    {
                        kind = NotificationKind.Comment;
                        mode = modeOf(member.UserId, NotificationKind.Comment);
                    }
                    planned.Add(new(member, kind, mode));
                }
                break;
        }

        return [.. planned.Where(p => p.Mode != NotificationMode.Off)];
    }
}