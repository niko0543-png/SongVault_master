using System.Globalization;
using System.Text.RegularExpressions;

namespace SongVault.Api.Observability;

/// <summary>
/// Retire d'un texte ce qui ne doit jamais partir dans la télémétrie (ADR 0025) :
/// adresses e-mail, jetons des liens (partage, invitation) et valeurs de la chaîne de requête.
/// </summary>
public static partial class TelemetryRedactor
{
    public const string EmailPlaceholder = "[e-mail]";
    public const string TokenPlaceholder = "{token}";
    public const string QueryPlaceholder = "Redacted";      // le mot qu'emploie déjà OpenTelemetry pour url.query

    [GeneratedRegex(@"[A-Z0-9._%+\-]+@[A-Z0-9\-]+(?:\.[A-Z0-9\-]+)*\.[A-Z]{2,}", RegexOptions.IgnoreCase)]
    private static partial Regex Email();

    // Le segment qui suit /api/share/, /api/invitations/, /s/ ou /invite/ est un jeton secret (#3, #13)
    [GeneratedRegex(@"(/(?:api/share|api/invitations|s|invite)/)(?!\{token\})[^/?#\s""']+", RegexOptions.IgnoreCase)]
    private static partial Regex PathToken();

    // ?token=…, &code=…, ?email=… : jetons de désinscription, de confirmation, de réinitialisation
    [GeneratedRegex(@"([?&][^=&#\s""']+=)[^&#\s""']*")]
    private static partial Regex QueryValue();

    /// <summary>Le texte sans e-mail, sans jeton ni valeur de requête ; le même objet s'il n'y avait rien à retirer.</summary>
    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var result = Email().Replace(text, EmailPlaceholder);
        result = PathToken().Replace(result, "$1" + TokenPlaceholder);
        result = QueryValue().Replace(result, "$1" + QueryPlaceholder);
        return result == text ? text : result;
    }

    /// <summary>
    /// Une valeur d'attribut (journal, trace) après Redact. Nombres, booléens, dates et GUID passent tels quels ;
    /// les autres objets (PathString, Uri…) sont convertis en texte s'il y a quelque chose à retirer.
    /// </summary>
    public static object? RedactValue(object? value)
    {
        switch (value)
        {
            case null or bool or byte or short or int or long or float or double or decimal
                or DateTime or DateTimeOffset or TimeSpan or Guid or Enum:
                return value;
            case string s:
                return Redact(s);
            default:
                var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                var redacted = Redact(text);
                return ReferenceEquals(redacted, text) ? value : redacted;
        }
    }
}