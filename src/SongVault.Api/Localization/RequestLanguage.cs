using Microsoft.Net.Http.Headers;

namespace SongVault.Api.Localization;

public enum Language { French, English }

/// <summary>
/// #26 : la langue des messages d'erreur, lue dans l'en-tête Accept-Language que le front envoie à chaque appel.
/// Seuls les messages changent : la culture de la requête (dates, nombres, e-mails) reste celle d'avant.
/// </summary>
public static class RequestLanguage
{
    public static Language From(HttpRequest request)
    {
        IList<StringWithQualityHeaderValue> tags;
        try { tags = request.GetTypedHeaders().AcceptLanguage; }
        catch (FormatException) { return Language.French; }

        // À qualité égale, l'ordre de l'en-tête décide (OrderByDescending est stable)
        foreach (var tag in tags.OrderByDescending(t => t.Quality ?? 1))
        {
            var value = tag.Value.Value;
            if (value is null || value.Length < 2) continue;
            switch (value[..2].ToLowerInvariant())
            {
                case "fr": return Language.French;
                case "en": return Language.English;
            }
        }
        return Language.French;
    }
}
