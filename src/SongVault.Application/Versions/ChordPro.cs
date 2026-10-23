using System.Text.RegularExpressions;

namespace SongVault.Application.Versions;

/// <summary>
/// #25 : paroles au format ChordPro, « [Am]Je marche [F]seul » (ADR 0032). Même grammaire d'accord que
/// chords.ts côté front : « [Refrain] » ou « [x2] » ne sont pas des accords et restent du texte.
/// Le front affiche et transpose ; ici, on ne fait que retirer les accords pour un texte brut (#24).
/// </summary>
public static partial class ChordPro
{
    private static readonly HashSet<string> CommentDirectives =
        new(StringComparer.OrdinalIgnoreCase) { "c", "comment", "ci", "comment_italic", "cb", "comment_box" };

    public static bool IsChord(string text) => ChordPattern().IsMatch(text);

    public static bool HasChords(string? source)
        => source is not null && Bracket().Matches(source).Any(m => IsChord(m.Groups[1].Value));

    /// <summary>
    /// Les paroles sans les accords : une indication « {c: Refrain} » devient la ligne « Refrain », les autres
    /// directives disparaissent, une ligne qui ne portait que des accords aussi. Les lignes vides restent.
    /// </summary>
    public static string StripChords(string source)
    {
        var lines = new List<string>();
        foreach (var line in source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var directive = Directive().Match(line);
            if (directive.Success)
            {
                var text = directive.Groups[2].Value.Trim();
                if (CommentDirectives.Contains(directive.Groups[1].Value) && text.Length > 0) lines.Add(text);
                continue;
            }

            var hadChords = false;
            var stripped = Bracket().Replace(line, m =>
            {
                if (!IsChord(m.Groups[1].Value)) return m.Value;
                hadChords = true;
                return "";
            });
            if (!hadChords)
            {
                lines.Add(line);
                continue;
            }
            stripped = Spaces().Replace(stripped, " ").Trim();     // « seul [F] [G]sous » : un seul espace
            if (stripped.Length > 0) lines.Add(stripped);
        }
        return string.Join('\n', lines);
    }

    [GeneratedRegex(@"^([A-G](?:#|b)?)((?:maj|min|dim|aug|sus|add|m|M|°|ø|Δ|\+|-|\d|#|b|\(|\)|,)*)(?:/([A-G](?:#|b)?))?$")]
    private static partial Regex ChordPattern();

    [GeneratedRegex(@"\[([^\]\n]*)\]")]
    private static partial Regex Bracket();

    [GeneratedRegex(@"^\s*\{\s*([a-z_]+)\s*(?::\s*(.*?))?\s*\}\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Directive();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex Spaces();
}