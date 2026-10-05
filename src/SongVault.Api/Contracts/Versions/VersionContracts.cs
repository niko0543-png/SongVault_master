using System.ComponentModel.DataAnnotations;

using SongVault.Api.Contracts.Files;
using SongVault.Application.Versions;
using SongVault.Domain.Songs;

namespace SongVault.Api.Contracts.Versions;

public sealed record CreateSongVersionRequest(
    [Required, MaxLength(SongVersion.TitleMaxLength)] string Title,
    [Required] SongVersionStatus? Status,
    [MaxLength(SongVersion.NotesMaxLength)] string? Notes,
    [MaxLength(SongVersion.LyricsMaxLength)] string? Lyrics,
    [Range(SongVersion.MinBpm, SongVersion.MaxBpm)] int? Bpm,     // NOUVEAU
    [MaxLength(MusicalKey.MaxLength)] string? Key);               // NOUVEAU

public sealed record UpdateSongVersionRequest(
    [Required, MaxLength(SongVersion.TitleMaxLength)] string Title,
    [Required] SongVersionStatus? Status,
    [MaxLength(SongVersion.NotesMaxLength)] string? Notes,
    [MaxLength(SongVersion.LyricsMaxLength)] string? Lyrics,
    [Range(SongVersion.MinBpm, SongVersion.MaxBpm)] int? Bpm,
    [MaxLength(MusicalKey.MaxLength)] string? Key);

public sealed record SongVersionResponse(
    Guid Id, Guid SongId, int Number, string Title, SongVersionStatus Status,
    string? Notes, string? Lyrics,
    int? Bpm, string? Key,                                         // NOUVEAU
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<SongFileResponse> Files)
{
    public static SongVersionResponse From(SongVersionDto d) =>
        new(d.Id, d.SongId, d.Number, d.Title, d.Status, d.Notes, d.Lyrics,
            d.Bpm, d.Key,
            d.CreatedAt, d.UpdatedAt,
            [.. d.Files.Select(SongFileResponse.From)]);
}

public sealed record SongVersionSummaryResponse(Guid Id, int Number, string Title, SongVersionStatus Status, DateTimeOffset CreatedAt)
{
    public static SongVersionSummaryResponse From(SongVersionSummaryDto d) => new(d.Id, d.Number, d.Title, d.Status, d.CreatedAt);
}