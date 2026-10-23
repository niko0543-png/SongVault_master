using SongVault.Application.Songs;
using System.Text.Json.Serialization;

namespace SongVault.Api.Contracts.Songs;

/// <summary>
/// Badge : pastille « nouveau » ou « modifié » pour l'utilisateur qui lit la liste ; toujours None ailleurs.
/// #34 : TablatureVersionId et TablatureFileId ouvrent la tablature depuis la liste ; null ailleurs ou sans tablature lisible.
/// #38 : ReleaseId et ReleasedAt, renseignés quand le morceau est sorti (il est alors dans l'onglet Sorties).
/// </summary>
public sealed record SongResponse(
    Guid Id, string Title, string? Artist, string? Description,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, [property: JsonConverter(typeof(JsonStringEnumConverter<SongBadge>))] SongBadge Badge,
    Guid? TablatureVersionId, Guid? TablatureFileId, Guid? ReleaseId, DateTimeOffset? ReleasedAt)
{
    public static SongResponse From(SongDto dto) =>
        new(dto.Id, dto.Title, dto.Artist, dto.Description, dto.CreatedAt, dto.UpdatedAt, dto.Badge,
            dto.TablatureVersionId, dto.TablatureFileId, dto.ReleaseId, dto.ReleasedAt);
}