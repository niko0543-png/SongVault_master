using SongVault.Domain.Trash;

namespace SongVault.Application.Trash;

/// <summary>Une ligne de la corbeille. SizeBytes : la place qu'elle occupe encore, jusqu'à la purge.</summary>
public sealed record TrashItemDto(
    TrashItemKind Kind, Guid Id, Guid SongId, string SongTitle, int? VersionNumber, string? VersionTitle,
    string? FileName, long SizeBytes, DateTimeOffset DeletedAt, string? DeletedById, string? DeletedByEmail)
{
    public DateTimeOffset PurgeAt => TrashPolicy.PurgeAt(DeletedAt);
}

public sealed record TrashEntry(TrashItemKind Kind, Guid Id);

/// <summary>Ce que la purge efface : les fichiers du stockage (originaux et MP3 d'aperçu), puis les lignes.</summary>
public sealed record PurgeTarget(Guid BandId, string Label, IReadOnlyList<string> StorageKeys);