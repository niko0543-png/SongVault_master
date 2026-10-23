using SongVault.Domain.Songs;

namespace SongVault.Application.Versions;

/// <param name="CopyFromVersionId">Version d'où reprendre des fichiers (ADR 0047) ; requise avec <paramref name="CopyFileIds"/>.</param>
/// <param name="CopyFileIds">Fichiers en place de cette version à copier dans la nouvelle ; vide ou null : aucun.</param>
public sealed record CreateSongVersionCommand(
    Guid SongId, string Title, SongVersionStatus Status, string? Notes, string? Lyrics,
    int? Bpm = null, string? Key = null, Guid? CopyFromVersionId = null, IReadOnlyList<Guid>? CopyFileIds = null);

public sealed record UpdateSongVersionCommand(
    Guid SongId, Guid VersionId, string Title, SongVersionStatus Status, string? Notes, string? Lyrics,
    int? Bpm = null, string? Key = null);