using System.IO.Compression;

namespace SongVault.Application.Releases.Exports;

/// <summary>
/// Écrit le ZIP entrée par entrée directement dans le flux de sortie (#24) : un seul fichier ouvert à la fois,
/// rien de gardé en mémoire ni sur le disque. L'audio et la pochette sont stockés tels quels (déjà compressés
/// ou sans gain), le texte est compressé.
/// </summary>
public static class ReleaseArchiveWriter
{
    public static async Task WriteAsync(
        Stream output, IReadOnlyList<ReleaseArchiveEntry> entries, DateTimeOffset generatedAt,
        Func<string, CancellationToken, Task<Stream>> openFile, CancellationToken ct)
    {
        await using var zip = await ZipArchive.CreateAsync(output, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: null, ct);

        foreach (var entry in entries)
        {
            var zipEntry = zip.CreateEntry(entry.Path,
                entry.StorageKey is null ? CompressionLevel.Optimal : CompressionLevel.NoCompression);
            zipEntry.LastWriteTime = generatedAt;
            await using var target = await zipEntry.OpenAsync(ct);
            if (entry.Content is not null)
            {
                await target.WriteAsync(entry.Content, ct);
                continue;
            }
            await using var source = await openFile(entry.StorageKey!, ct);
            await source.CopyToAsync(target, ct);
        }
    }
}