using System.Text.RegularExpressions;

namespace SongVault.Api.Localization;

/// <summary>
/// #26 : retrouve un message français dans ErrorCatalog et le rend dans la langue demandée, avec son code.
/// Les parties variables (un titre, une adresse, un nombre) passent telles quelles ; un nom de champ est traduit.
/// </summary>
public static class ErrorTranslator
{
    private sealed record Entry(ErrorMessage Message, Regex Pattern);

    private static readonly Regex Hole = new(@"\{(\d+)\}", RegexOptions.CultureInvariant);

    // Le modèle le plus précis d'abord : « {0} '{1}' introuvable. » ne doit pas avaler un message plus long
    private static readonly Entry[] Entries = [.. ErrorCatalog.Messages
        .Select(m => new Entry(m, ToPattern(m.French)))
        .OrderByDescending(e => Hole.Replace(e.Message.French, "").Length)];

    private static Regex ToPattern(string template)
    {
        var pattern = string.Concat(Regex.Split(template, @"(\{\d+\})").Select(part =>
            Hole.Match(part) is { Success: true } hole && hole.Value == part
                ? $"(?<p{hole.Groups[1].Value}>.+?)"
                : Regex.Escape(part)));
        return new Regex($"^{pattern}$", RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    }

    /// <summary>Le message dans la langue voulue, et son code ; (null, message) s'il n'est pas au catalogue.</summary>
    public static (string? Code, string Text) Translate(string message, Language language)
    {
        foreach (var entry in Entries)
        {
            Match match;
            try { match = entry.Pattern.Match(message); }
            catch (RegexMatchTimeoutException) { continue; }
            if (!match.Success) continue;
            if (language == Language.French) return (entry.Message.Code, message);

            var text = Hole.Replace(entry.Message.English, hole =>
            {
                var value = match.Groups[$"p{hole.Groups[1].Value}"].Value;
                return ErrorCatalog.FieldNames.TryGetValue(value, out var field) ? field : TranslateList(value);
            });
            return (entry.Message.Code, text);
        }
        return (null, message);
    }

    // Une liste « a, b » dont un élément est lui-même au catalogue (« piste 2 « Nocturne » ») : chacun est traduit
    private static string TranslateList(string value)
        => string.Join(", ", value.Split(", ").Select(item => Translate(item, Language.English).Text));
}
