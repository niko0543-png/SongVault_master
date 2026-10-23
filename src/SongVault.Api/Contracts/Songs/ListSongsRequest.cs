using System.ComponentModel.DataAnnotations;

using SongVault.Domain.Songs;

namespace SongVault.Api.Contracts.Songs;

public sealed class ListSongsRequest
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 50)] public int PageSize { get; init; } = 20;
    [MaxLength(100)] public string? Search { get; init; }
    public SongVersionStatus? Status { get; init; }
    /// <summary>#38 : true = morceaux sortis, false = en travail (la liste des morceaux), absent = tous.</summary>
    public bool? Released { get; init; }
}