using System.ComponentModel.DataAnnotations;

namespace SongVault.Api.Contracts.Songs;

public sealed class ListSongsRequest
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 50)] public int PageSize { get; init; } = 20;
}