using System.ComponentModel.DataAnnotations;

namespace SongVault.Api.Contracts.Songs;

public sealed record CreateSongRequest(
    [Required, MaxLength(200)] string Title,
    [MaxLength(200)] string? Artist,
    [MaxLength(2000)] string? Description);
