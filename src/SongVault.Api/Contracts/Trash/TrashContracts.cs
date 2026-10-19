using SongVault.Application.Trash;
using SongVault.Domain.Trash;

namespace SongVault.Api.Contracts.Trash;

/// <summary>
/// Une ligne de la corbeille. VersionNumber et VersionTitle : pour une version ou un fichier ; FileName : pour un fichier.
/// DeletedByEmail null avec un DeletedById : compte supprimé. PurgeAt : date de la suppression définitive.
/// </summary>
public sealed record TrashItemResponse(
    TrashItemKind Kind, Guid Id, Guid SongId, string SongTitle, int? VersionNumber, string? VersionTitle,
    string? FileName, long SizeBytes, DateTimeOffset DeletedAt, string? DeletedById, string? DeletedByEmail,
    DateTimeOffset PurgeAt)
{
    public static TrashItemResponse From(TrashItemDto d) =>
        new(d.Kind, d.Id, d.SongId, d.SongTitle, d.VersionNumber, d.VersionTitle, d.FileName, d.SizeBytes,
            d.DeletedAt, d.DeletedById, d.DeletedByEmail, d.PurgeAt);
}