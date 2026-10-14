using SongVault.Application.Songs;
using System.Text.Json.Serialization;

namespace SongVault.Api.Contracts.Songs;

/// <summary>Badge : pastille « nouveau » ou « modifié » pour l'utilisateur qui lit la liste ; toujours None ailleurs.</summary>
public sealed record SongResponse(
    Guid Id, string Title, string? Artist, string? Description,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, [property: JsonConverter(typeof(JsonStringEnumConverter<SongBadge>))] SongBadge Badge)
{
    public static SongResponse From(SongDto dto) =>
        new(dto.Id, dto.Title, dto.Artist, dto.Description, dto.CreatedAt, dto.UpdatedAt, dto.Badge);
}