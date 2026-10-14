using System.Text.RegularExpressions;

namespace SongVault.Domain.Comments;

/// <summary>
/// Mentions dans un commentaire (#12) : « @ » suivi de l'adresse e-mail d'un membre, tant que les comptes
/// n'ont pas de nom d'affichage. « Merci @ana@exemple.fr ! » mentionne ana@exemple.fr.
/// </summary>
public static partial class CommentMentions
{
    // @ en début de texte ou après un blanc ou une ponctuation ouvrante, pour ne pas lire « ana@exemple.fr » seul
    [GeneratedRegex(@"(?<=^|[\s(\[«""'])@(?<email>[^\s@]+@[^\s@]+\.[^\s@]+)", RegexOptions.CultureInvariant)]
    private static partial Regex Mention();

    /// <summary>Adresses mentionnées, en minuscules, sans doublon. La ponctuation qui suit l'adresse est ignorée.</summary>
    public static IReadOnlySet<string> FindEmails(string body)
    {
        var emails = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Mention().Matches(body))
        {
            var email = match.Groups["email"].Value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '»', '"', '\'');
            if (email.Contains('@') && email.LastIndexOf('.') > email.IndexOf('@'))
                emails.Add(email.ToLowerInvariant());
        }
        return emails;
    }
}