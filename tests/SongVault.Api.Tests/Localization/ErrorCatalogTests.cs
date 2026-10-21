using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

using SongVault.Api.Localization;

namespace SongVault.Api.Tests.Localization;

/// <summary>
/// #26 : tout message d'erreur écrit dans le code (exceptions renvoyées au client, ModelState, titres) a sa ligne
/// dans ErrorCatalog. Le test lit les sources : un message ajouté sans traduction le fait échouer, avec la liste.
/// </summary>
public sealed partial class ErrorCatalogTests
{
    private static readonly string[] Projects = ["SongVault.Domain", "SongVault.Application", "SongVault.Api", "SongVault.Infrastructure"];

    // Exceptions dont le message part vers le client (voir GlobalExceptionHandler)
    [GeneratedRegex(@"(?:throw new|: )(?:Domain|Conflict|Forbidden|PlanLimit|UnsupportedFile|FileNotReadable|LinkExpired|BillingUnavailable|InvalidPaymentEvent|EmailDelivery)Exception\(|: Exception\(|AddModelError\(|\b(?:Title|Detail) = |\b(?:title|detail): |=> \(StatusCodes\.\w+, ")]
    private static partial Regex MessageSite();

    [Fact]
    public void Chaque_message_ecrit_dans_le_code_est_au_catalogue()
    {
        var missing = new List<string>();
        foreach (var (file, text) in Sources())
            foreach (Match site in MessageSite().Matches(text))
                foreach (var segments in LiteralsFrom(text, site.Index + site.Length - (site.Value.EndsWith('"') ? 1 : 0)))
                    if (LooksFrench(string.Concat(segments)) && !Covered(segments))
                        missing.Add($"{file}: {string.Join("{…}", segments)}");

        Assert.True(missing.Count == 0, "Messages sans ligne dans ErrorCatalog :\n" + string.Join("\n", missing.Distinct()));
    }

    [Fact]
    public void Le_test_lit_bien_les_sources()
        => Assert.Contains(Sources(), s => s.File.EndsWith("Text.cs") && s.Text.Contains("est obligatoire"));

    private static bool LooksFrench(string text)
        => text.Length > 3 && Regex.IsMatch(text, "[a-zà-ÿ]{2,}") && Regex.IsMatch(text, @"[àâçéèêëîïôûùüœ«»]|\s");

    /// <summary>Un message est couvert si un modèle du catalogue contient ses parties fixes, dans l'ordre.</summary>
    private static bool Covered(IReadOnlyList<string> segments)
        => ErrorCatalog.Messages.Any(m =>
        {
            var at = 0;
            foreach (var segment in segments.Where(s => s.Trim().Length >= 3))
            {
                var found = m.French.IndexOf(segment, at, StringComparison.Ordinal);
                if (found < 0) return false;
                at = found + segment.Length;
            }
            return true;
        });

    /// <summary>
    /// Les littéraux "…" et $"…" de l'appel qui commence à start, jusqu'à sa parenthèse fermante. Pour $"…", les
    /// parties fixes entre les {…} ; un littéral placé dans un {…} (un séparateur, un « s » de pluriel) est ignoré.
    /// </summary>
    private static IEnumerable<IReadOnlyList<string>> LiteralsFrom(string text, int start)
    {
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/') { i = text.IndexOf('\n', i); if (i < 0) yield break; continue; }
            if (c == '(' || c == '[' || c == '{') depth++;
            else if (c == ')' || c == ']' || c == '}' || c == ';' || (c == ',' && depth == 0 && text[start - 1] == ' '))
            {
                if (depth == 0) yield break;
                depth--;
            }
            else if (c == '"' || (c == '$' && i + 1 < text.Length && text[i + 1] == '"'))
            {
                var interpolated = c == '$';
                if (interpolated) i++;
                yield return ReadLiteral(text, ref i, interpolated);
            }
        }
    }

    private static List<string> ReadLiteral(string text, ref int i, bool interpolated)
    {
        var segments = new List<string>();
        var current = new StringBuilder();
        for (i++; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\\') { current.Append(text[++i] switch { 'n' => '\n', var x => x }); continue; }
            if (c == '"') break;
            if (interpolated && c == '{')
            {
                if (text[i + 1] == '{') { current.Append('{'); i++; continue; }
                segments.Add(current.ToString()); current.Clear();
                SkipHole(text, ref i);
                continue;
            }
            if (interpolated && c == '}' && text[i + 1] == '}') { current.Append('}'); i++; continue; }
            current.Append(c);
        }
        segments.Add(current.ToString());
        return segments;
    }

    private static void SkipHole(string text, ref int i)
    {
        var depth = 0;
        for (; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return;
            else if (c == '"' || (c == '$' && text[i + 1] == '"'))
            {
                var interpolated = c == '$';
                if (interpolated) i++;
                ReadLiteral(text, ref i, interpolated);
            }
        }
    }

    private static IEnumerable<(string File, string Text)> Sources([CallerFilePath] string here = "")
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", "src"));
        foreach (var project in Projects)
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                    file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}") ||
                    file.EndsWith("ErrorCatalog.cs")) continue;
                yield return (Path.GetRelativePath(root, file), File.ReadAllText(file));
            }
    }
}
