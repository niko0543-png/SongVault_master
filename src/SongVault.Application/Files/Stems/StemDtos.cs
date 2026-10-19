using SongVault.Domain.Files;

namespace SongVault.Application.Files.Stems;

/// <summary>
/// Une piste du mélangeur (#23, ADR 0030). Playable : l'antivirus l'a déclarée saine et son aperçu MP3 est prêt ;
/// le navigateur ne charge que celles-là, et DurationMs sert à estimer la mémoire avant de les décoder.
/// </summary>
public sealed record StemDto(Guid FileId, string Name, int Position, string OriginalFileName, int? DurationMs, bool Playable)
{
    public static StemDto Create(Guid fileId, string name, int position, string originalFileName,
        FileScanStatus? scan, int? readyPreviewDurationMs)
        => new(fileId, name, position, originalFileName, readyPreviewDurationMs,
            (scan is FileScanStatus.Clean or FileScanStatus.NotScanned) && readyPreviewDurationMs is not null);
}

/// <summary>Toutes les pistes de la version, dans l'ordre ; elles remplacent les anciennes.</summary>
public sealed record SetStemsCommand(Guid SongId, Guid VersionId, IReadOnlyList<StemChoice> Stems);