using SongVault.Application.Songs;

namespace SongVault.Api.Contracts.Songs;

public sealed record SongResponse(
    Guid Id, string Title, string? Artist, string? Description,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static SongResponse From(SongDto dto) =>
        new(dto.Id, dto.Title, dto.Artist, dto.Description, dto.CreatedAt, dto.UpdatedAt);
}