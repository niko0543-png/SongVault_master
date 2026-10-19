using System.Globalization;
using System.Text;

using SongVault.Application.Files;
using SongVault.Application.Releases;
using SongVault.Domain.Songs;

namespace SongVault.Application.Songs.Rights;

/// <summary>
/// Export CSV des crédits pour Excel en français (#22, ADR 0029) : séparateur point-virgule, UTF-8 avec BOM,
/// fins de ligne CRLF, parts en décimal à virgule (33,33). Écrit à la main, sans paquet : trois règles de
/// guillemets suffisent pour ce qu'on écrit.
/// </summary>
public static class CreditsCsv
{
    public const string ContentType = "text/csv; charset=utf-8";

    private static readonly string[] SongHeader = ["Morceau", "ISWC", "Ayant droit", "Rôle", "Part (%)"];
    private static readonly string[] ReleaseHeader = ["Piste", "Morceau", "Version", "ISRC", "ISWC", "Ayant droit", "Rôle", "Part (%)"];

    /// <summary>Une ligne par crédit ; un morceau sans crédit garde une ligne, pour son titre et son ISWC.</summary>
    public static byte[] ForSong(Song song)
    {
        var rows = new List<string?[]> { SongHeader };
        foreach (var credit in CreditsOrNone(song))
            rows.Add([song.Title, song.Iswc?.Value, credit?.Name, Role(credit), Share(credit)]);
        return Write(rows);
    }

    /// <summary>Une ligne par crédit de chaque piste, avec l'ISRC de la version choisie et l'ISWC du morceau.</summary>
    public static byte[] ForRelease(ReleaseDto release, IReadOnlyList<Song> songs)
    {
        var byId = songs.ToDictionary(s => s.Id);
        var rows = new List<string?[]> { ReleaseHeader };
        foreach (var track in release.Tracks)
        {
            var song = byId.GetValueOrDefault(track.SongId);
            var version = song?.FindVersion(track.VersionId);
            var versionLabel = track.Versions.FirstOrDefault(v => v.Id == track.VersionId) is { } v ? $"v{v.Number} {v.Title}" : null;
            IEnumerable<Credit?> credits = song is null ? [null] : CreditsOrNone(song);
            foreach (var credit in credits)
                rows.Add([track.TrackNumber.ToString(CultureInfo.InvariantCulture), track.SongTitle, versionLabel,
                    version?.Isrc?.Display, song?.Iswc?.Value, credit?.Name, Role(credit), Share(credit)]);
        }
        return Write(rows);
    }

    public static FileDownload File(string baseName, byte[] content)
        => new(new MemoryStream(content), ContentType, FileName(baseName));

    /// <summary>Le titre tel quel, sans les caractères interdits dans un nom de fichier.</summary>
    public static string FileName(string baseName)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['"', '<', '>', '|', ':', '*', '?', '\\', '/']).ToHashSet();
        var clean = new string([.. baseName.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c)]).Trim();
        return $"{clean}.csv";
    }

    /// <summary>Auteur, compositeur… : les mots que lit l'éditeur ou la SACEM.</summary>
    public static string RoleLabel(CreditRole role) => role switch
    {
        CreditRole.Author => "Auteur",
        CreditRole.Composer => "Compositeur",
        CreditRole.Arranger => "Arrangeur",
        CreditRole.Publisher => "Éditeur",
        _ => role.ToString(),
    };

    /// <summary>33,33 ; 12,5 ; 100 : la virgule d'Excel en français, sans zéros inutiles.</summary>
    public static string FormatShare(decimal share)
        => share.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');

    private static IEnumerable<Credit?> CreditsOrNone(Song song)
        => song.Credits.Count == 0 ? [null] : song.Credits.Cast<Credit?>();

    private static string? Role(Credit? credit) => credit is null ? null : RoleLabel(credit.Role);

    private static string? Share(Credit? credit) => credit is null ? null : FormatShare(credit.SharePercent);

    private static byte[] Write(IEnumerable<string?[]> rows)
    {
        var text = new StringBuilder();
        foreach (var row in rows)
            text.Append(string.Join(';', row.Select(Field))).Append("\r\n");
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(text.ToString())];
    }

    // Une cellule qui commence par = + - @ serait lue par Excel comme une formule : on la préfixe d'une apostrophe.
    // Une cellule avec ; " ou un retour à la ligne passe entre guillemets, les guillemets doublés.
    public static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        return value.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}