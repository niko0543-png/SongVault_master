using SongVault.Domain.Files;

namespace SongVault.Application.Files.Sync;

/// <summary>Le calage d'une tablature (#29, ADR 0036) : la maquette suivie et les points, dans l'ordre de la lecture.</summary>
public sealed record TabSyncDto(Guid AudioFileId, string AudioFileName, IReadOnlyList<TabSyncPoint> Points);

public sealed record SetTabSyncCommand(Guid SongId, Guid VersionId, Guid FileId, Guid AudioFileId, IReadOnlyList<TabSyncPoint> Points);