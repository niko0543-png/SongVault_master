using SongVault.Domain.Songs;

namespace SongVault.Application.Songs;

public sealed record SongDto(
    Guid Id, string Title, string? Artist, string? Description,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static SongDto From(Song song) =>
        new(song.Id, song.Title, song.Artist, song.Description, song.CreatedAt, song.UpdatedAt);
}