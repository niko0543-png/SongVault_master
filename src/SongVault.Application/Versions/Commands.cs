using SongVault.Domain.Songs;

namespace SongVault.Application.Versions;

public sealed record CreateSongVersionCommand(
    Guid SongId, string Title, SongVersionStatus Status, string? Notes, string? Lyrics,
    int? Bpm = null, string? Key = null);

public sealed record UpdateSongVersionCommand(
    Guid SongId, Guid VersionId, string Title, SongVersionStatus Status, string? Notes, string? Lyrics,
    int? Bpm = null, string? Key = null);