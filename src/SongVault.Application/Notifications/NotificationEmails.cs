using System.Net;
using System.Text;

using SongVault.Application.Abstractions;
using SongVault.Domain.Activity;
using SongVault.Domain.Notifications;

namespace SongVault.Application.Notifications;

/// <summary>E-mails de notification (ADR 0018). Chacun porte un lien de désinscription et l'en-tête List-Unsubscribe.</summary>
public sealed class NotificationEmails(string publicUrl, IUnsubscribeTokens tokens)
{
    private const int ExcerptLength = 300;

    private readonly string _url = publicUrl.TrimEnd('/');

    /// <summary>Un e-mail pour un événement.</summary>
    public EmailMessage Immediate(NotificationRecipient to, NotificationKind kind, NotificationEvent e)
    {
        var sentence = Sentence(e, kind);
        var link = VersionLink(e);
        var excerpt = Excerpt(e.CommentBody);
        var (unsubscribe, headers) = Unsubscribe(to.UserId, kind);
        var footer = $"Vous recevez cet e-mail car les notifications « {Label(kind)} » sont activées sur votre compte.";

        var text = new StringBuilder()
            .AppendLine($"Groupe « {e.BandName} »").AppendLine()
            .AppendLine($"{sentence}.");
        if (excerpt is not null) text.AppendLine().AppendLine($"« {excerpt} »");
        if (link is not null) text.AppendLine().AppendLine($"Ouvrir : {link}");
        text.AppendLine().AppendLine("--")
            .AppendLine(footer)
            .AppendLine($"Ne plus les recevoir : {unsubscribe}")
            .AppendLine($"Choisir mes notifications : {_url}/account");

        var html = new StringBuilder()
            .Append($"<p style=\"color:#666\">Groupe « {H(e.BandName)} »</p>")
            .Append($"<p>{H(sentence)}.</p>");
        if (excerpt is not null) html.Append($"<blockquote>{H(excerpt)}</blockquote>");
        if (link is not null) html.Append($"<p><a href=\"{H(link)}\">Ouvrir</a></p>");
        html.Append($"<p style=\"color:#666;font-size:small\">{H(footer)} ")
            .Append($"<a href=\"{H(unsubscribe)}\">Ne plus les recevoir</a> · ")
            .Append($"<a href=\"{H(_url)}/account\">Choisir mes notifications</a></p>");

        return new EmailMessage(to.Email, Subject(kind, e), html.ToString(), text.ToString(), headers);
    }

    /// <summary>Le résumé quotidien : les événements regroupés par groupe, dans l'ordre où ils sont arrivés.</summary>
    public EmailMessage Digest(string userId, string email, IReadOnlyList<DigestEntry> entries)
    {
        var (unsubscribe, headers) = Unsubscribe(userId, null);
        var count = entries.Count;
        var subject = count == 1 ? "SongVault : 1 nouveauté dans vos groupes" : $"SongVault : {count} nouveautés dans vos groupes";

        var text = new StringBuilder().AppendLine("Bonjour,").AppendLine()
            .AppendLine("Voici ce qui s'est passé dans vos groupes depuis le dernier résumé.");
        var html = new StringBuilder().Append("<p>Bonjour,</p>")
            .Append("<p>Voici ce qui s'est passé dans vos groupes depuis le dernier résumé.</p>");

        foreach (var band in entries.GroupBy(x => (x.Event.BandId, x.Event.BandName)))
        {
            text.AppendLine().AppendLine($"Groupe « {band.Key.BandName} »");
            html.Append($"<h3>Groupe « {H(band.Key.BandName)} »</h3><ul>");
            foreach (var entry in band)
            {
                var e = entry.Event;
                var sentence = Sentence(e, entry.Kind);
                var link = VersionLink(e);
                text.AppendLine(link is null ? $"- {sentence}" : $"- {sentence} : {link}");
                html.Append(link is null
                    ? $"<li>{H(sentence)}</li>"
                    : $"<li><a href=\"{H(link)}\">{H(sentence)}</a></li>");
            }
            html.Append("</ul>");
        }

        const string footer = "Vous recevez ce résumé car certaines notifications sont réglées sur « résumé quotidien ».";
        text.AppendLine().AppendLine("--")
            .AppendLine(footer)
            .AppendLine($"Ne plus recevoir aucun e-mail de notification : {unsubscribe}")
            .AppendLine($"Choisir mes notifications : {_url}/account");
        html.Append($"<p style=\"color:#666;font-size:small\">{H(footer)} ")
            .Append($"<a href=\"{H(unsubscribe)}\">Ne plus recevoir aucun e-mail de notification</a> · ")
            .Append($"<a href=\"{H(_url)}/account\">Choisir mes notifications</a></p>");

        return new EmailMessage(email, subject, html.ToString(), text.ToString(), headers);
    }

    /// <summary>« ana@exemple.fr a ajouté la v4 « Démo » de « Nocturne » », sans point final.</summary>
    public static string Sentence(NotificationEvent e, NotificationKind kind)
    {
        var actor = e.ActorEmail ?? "Un compte supprimé";
        var version = e.VersionTitle is { Length: > 0 } title ? $"la v{e.VersionNumber} « {title} »" : $"la v{e.VersionNumber}";
        var file = $"{e.FileName} (v{e.VersionNumber} de « {e.SongTitle} »)";
        return kind switch
        {
            NotificationKind.NewVersion => $"{actor} a ajouté {version} de « {e.SongTitle} »",
            NotificationKind.Mention => $"{actor} vous a mentionné dans un commentaire sur {file}",
            _ when e.Type == ActivityType.CommentReplied => $"{actor} a répondu à un commentaire sur {file}",
            _ => $"{actor} a commenté {file}",
        };
    }

    public static string Label(NotificationKind kind) => kind switch
    {
        NotificationKind.NewVersion => "nouvelle version",
        NotificationKind.Comment => "commentaires",
        NotificationKind.Mention => "mentions",
        _ => kind.ToString(),
    };

    private static string Subject(NotificationKind kind, NotificationEvent e) => kind switch
    {
        NotificationKind.NewVersion => $"« {e.SongTitle} » : nouvelle version v{e.VersionNumber}",
        NotificationKind.Mention => $"{e.ActorEmail ?? "Un compte supprimé"} vous a mentionné sur « {e.SongTitle} »",
        _ when e.Type == ActivityType.CommentReplied => $"« {e.SongTitle} » : nouvelle réponse",
        _ => $"« {e.SongTitle} » : nouveau commentaire",
    };

    private string? VersionLink(NotificationEvent e)
        => e.VersionId is { } versionId ? $"{_url}/b/{e.BandId}/songs/{e.SongId}/versions/{versionId}" : null;

    /// <summary>Lien vers la page de désinscription, et en-têtes de désinscription en un clic (RFC 8058).</summary>
    private (string Link, IReadOnlyDictionary<string, string> Headers) Unsubscribe(string userId, NotificationKind? kind)
    {
        var token = Uri.EscapeDataString(tokens.Create(userId, kind));
        var headers = new Dictionary<string, string>
        {
            ["List-Unsubscribe"] = $"<{_url}/api/notifications/unsubscribe?token={token}>",
            ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
        };
        return ($"{_url}/unsubscribe?token={token}", headers);
    }

    private static string? Excerpt(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        var flat = string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= ExcerptLength ? flat : $"{flat[..ExcerptLength]}…";
    }

    private static string H(string value) => WebUtility.HtmlEncode(value);
}