using System.ComponentModel.DataAnnotations;

using SongVault.Application.Versions;
using SongVault.Domain.Songs;

namespace SongVault.Api.Contracts.Versions;

public sealed record CreateSongVersionRequest(
    [Required, MaxLength(200)] string Title,
    [Required] SongVersionStatus? Status,          // nullable + Required : un statut absent donne 400, pas "Idea" par défaut
    [MaxLength(4000)] string? Notes,
    [MaxLength(20000)] string? Lyrics);

public sealed record UpdateSongVersionRequest(
    [Required, MaxLength(200)] string Title,
    [Required] SongVersionStatus? Status,
    [MaxLength(4000)] string? Notes,
    [MaxLength(20000)] string? Lyrics);

public sealed record SongVersionResponse(
    Guid Id, Guid SongId, int Number, string Title, SongVersionStatus Status,
    string? Notes, string? Lyrics, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static SongVersionResponse From(SongVersionDto d) =>
        new(d.Id, d.SongId, d.Number, d.Title, d.Status, d.Notes, d.Lyrics, d.CreatedAt, d.UpdatedAt);
}

public sealed record SongVersionSummaryResponse(Guid Id, int Number, string Title, SongVersionStatus Status, DateTimeOffset CreatedAt)
{
    public static SongVersionSummaryResponse From(SongVersionSummaryDto d) => new(d.Id, d.Number, d.Title, d.Status, d.CreatedAt);
}