using SongVault.Application.Files;
using SongVault.Domain.Songs;

namespace SongVault.Application.Versions;

public sealed record SongVersionDto(
    Guid Id, Guid SongId, int Number, string Title, SongVersionStatus Status,
    string? Notes, string? Lyrics, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<SongFileDto> Files)
{
    public static SongVersionDto From(SongVersion v) =>
        new(v.Id, v.SongId, v.Number, v.Title, v.Status, v.Notes, v.Lyrics, v.CreatedAt, v.UpdatedAt,
            [.. v.Files.Select(SongFileDto.From)]);
}

public sealed record SongVersionSummaryDto(Guid Id, int Number, string Title, SongVersionStatus Status, DateTimeOffset CreatedAt);

