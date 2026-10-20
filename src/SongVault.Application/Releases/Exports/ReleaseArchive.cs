using System.Text;

using SongVault.Application.Common.Exceptions;
using SongVault.Application.Songs.Rights;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Application.Releases.Exports;

/// <summary>Ce que lit la tâche : la sortie (comme l'API la montre), ses morceaux, sa pochette et ses fichiers audio.</summary>
public sealed record ReleaseArchiveSource(
    ReleaseDto Release, string? CoverStorageKey, string? CoverContentType,
    IReadOnlyList<Song> Songs, IReadOnlyList<ReleaseArchiveFile> Files);

/// <summary>Un fichier audio d'une version choisie ; ScanStatus null : pas d'analyse connue (traité comme en cours).</summary>
public sealed record ReleaseArchiveFile(
    Guid VersionId, string FileName, string StorageKey, string? StemName, int? StemPosition, FileScanStatus? ScanStatus);

/// <summary>Une entrée du ZIP : un fichier du stockage (StorageKey) ou un contenu écrit ici (Content).</summary>
public sealed record ReleaseArchiveEntry(string Path, string? StorageKey, byte[]? Content);

/// <summary>
/// Plan de l'archive d'une sortie (#24, ADR 0031), sans rien lire ni écrire :
/// <code>
/// Titre de la sortie/
///   01 - Premier morceau/fichiers audio de la version choisie
///   01 - Premier morceau/Pistes séparées/1 - Voix.wav   (pistes du mélangeur, #23)
///   01 - Premier morceau/paroles.txt                     (si la version a des paroles)
///   credits.csv                                          (#22)
///   pochette.jpg                                         (si la sortie en a une)
/// </code>
/// </summary>
public static class ReleaseArchive
{
    public const string ContentType = "application/zip";
    public const string StemsFolder = "Pistes séparées";
    private const int NameMaxLength = 80;

    /// <summary>Refuse (409) une archive qui ne serait pas complète : sans piste, piste sans audio, fichier pas lisible.</summary>
    public static void EnsureReady(ReleaseArchiveSource source)
    {
        var tracks = source.Release.Tracks;
        if (tracks.Count == 0)
            throw new ConflictException("La sortie n'a aucune piste : ajoutez-en avant de préparer l'archive.");

        var withoutAudio = tracks.Where(t => !source.Files.Any(f => f.VersionId == t.VersionId)).ToList();
        if (withoutAudio.Count > 0)
            throw new ConflictException("Aucun fichier audio dans la version choisie pour : "
                + string.Join(", ", withoutAudio.Select(t => $"piste {t.TrackNumber} « {t.SongTitle} »")) + ".");

        var unreadable = (from t in tracks
                          from f in source.Files
                          where f.VersionId == t.VersionId && f.ScanStatus is not (FileScanStatus.Clean or FileScanStatus.NotScanned)
                          select $"« {f.FileName} » (piste {t.TrackNumber})").ToList();
        if (unreadable.Count > 0)
            throw new ConflictException("Fichiers pas encore disponibles (analyse antivirus en cours, échouée ou fichier bloqué) : "
                + string.Join(", ", unreadable) + ".");
    }

    public static IReadOnlyList<ReleaseArchiveEntry> Plan(ReleaseArchiveSource source)
    {
        var root = CleanName(source.Release.Title);
        var songs = source.Songs.ToDictionary(s => s.Id);
        var entries = new List<ReleaseArchiveEntry>();

        foreach (var track in source.Release.Tracks)
        {
            var folder = $"{root}/{track.TrackNumber:00} - {CleanName(track.SongTitle)}";
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "paroles.txt", StemsFolder };
            var files = source.Files.Where(f => f.VersionId == track.VersionId).ToList();

            foreach (var file in files.Where(f => f.StemPosition is null))
                entries.Add(new($"{folder}/{Unique(names, CleanName(file.FileName))}", file.StorageKey, null));

            var stemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var stem in files.Where(f => f.StemPosition is not null).OrderBy(f => f.StemPosition))
            {
                var name = $"{stem.StemPosition + 1} - {CleanName(stem.StemName ?? "Piste")}{Extension(stem.FileName)}";
                entries.Add(new($"{folder}/{StemsFolder}/{Unique(stemNames, name)}", stem.StorageKey, null));
            }

            var lyrics = songs.GetValueOrDefault(track.SongId)?.FindVersion(track.VersionId)?.Lyrics;
            if (!string.IsNullOrWhiteSpace(lyrics))
                entries.Add(new($"{folder}/paroles.txt", null, Utf8WithBom(lyrics)));
        }

        entries.Add(new($"{root}/credits.csv", null, CreditsCsv.ForRelease(source.Release, source.Songs)));
        if (source.CoverStorageKey is not null)
            entries.Add(new($"{root}/pochette{CoverExtension(source.CoverContentType)}", source.CoverStorageKey, null));
        return entries;
    }

    /// <summary>Nom proposé au téléchargement : le titre de la sortie.</summary>
    public static string FileName(string releaseTitle) => $"{CleanName(releaseTitle)}.zip";

    /// <summary>
    /// Un nom lisible sous Windows, macOS et Linux : sans caractère interdit ni de contrôle, sans point ni espace
    /// final (Windows les retire), 80 caractères au plus (chemins courts dans l'Explorateur).
    /// </summary>
    public static string CleanName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['"', '<', '>', '|', ':', '*', '?', '\\', '/']).ToHashSet();
        var clean = new string([.. name.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c)]).Trim();
        if (clean.Length > NameMaxLength)
        {
            var extension = Extension(clean);
            clean = extension.Length is > 0 and <= 10
                ? clean[..(NameMaxLength - extension.Length)].TrimEnd() + extension
                : clean[..NameMaxLength];
        }
        clean = clean.TrimEnd('.', ' ');
        return clean.Length == 0 ? "_" : clean;
    }

    // « prise.wav » déjà pris dans le dossier : « prise (2).wav », puis « prise (3).wav »
    private static string Unique(HashSet<string> taken, string name)
    {
        if (taken.Add(name)) return name;
        var extension = Extension(name);
        var stem = name[..^extension.Length];
        for (var n = 2; ; n++)
        {
            var candidate = $"{stem} ({n}){extension}";
            if (taken.Add(candidate)) return candidate;
        }
    }

    private static string Extension(string fileName) => Path.GetExtension(fileName).ToLowerInvariant();

    private static string CoverExtension(string? contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg",
    };

    // Le Bloc-notes de Windows et TextEdit lisent l'UTF-8 avec BOM et les fins de ligne CRLF
    private static byte[] Utf8WithBom(string text)
    {
        var crlf = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(crlf)];
    }
}