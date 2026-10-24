using SongVault.Domain.Files;

namespace SongVault.Application.Files.Splits;

/// <summary>Le fichier à découper, tel que la demande le contrôle.</summary>
/// <param name="DurationMs">Durée lue dans l'aperçu (ADR 0021) ; null tant qu'il n'est pas prêt.</param>
public sealed record AudioSplitSource(Guid FileId, string FileName, long SizeBytes, SongFileType FileType,
    FileScanStatus? Scan, AudioPreviewStatus? Preview, int? DurationMs);

/// <param name="SongId">Morceau existant qui reçoit une nouvelle version ; null : un nouveau morceau nommé Title.</param>
public sealed record AudioSplitPartCommand(int StartMs, int EndMs, string Title, Guid? SongId);

public sealed record RequestAudioSplitCommand(Guid SongId, Guid VersionId, Guid FileId, IReadOnlyList<AudioSplitPartCommand> Parts);

public sealed record AudioSplitPartDto(int StartMs, int EndMs, string Title, Guid? SongId, Guid? CreatedSongId, Guid? CreatedVersionId);

public sealed record AudioSplitDto(Guid Id, AudioSplitStatus Status, string? Error, DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt, IReadOnlyList<AudioSplitPartDto> Parts)
{
    public static AudioSplitDto From(AudioSplit split) => new(split.Id, split.Status, split.Error, split.RequestedAt,
        split.CompletedAt, [.. split.Parts.Select(p =>
            new AudioSplitPartDto(p.StartMs, p.EndMs, p.Title, p.SongId, p.CreatedSongId, p.CreatedVersionId))]);
}
