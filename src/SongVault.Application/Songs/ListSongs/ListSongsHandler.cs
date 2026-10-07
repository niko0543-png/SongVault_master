using SongVault.Application.Abstractions;
using SongVault.Application.Common;

namespace SongVault.Application.Songs.ListSongs;

public sealed class ListSongsHandler(ISongRepository songs)
{
    public const int MaxPageSize = 50;
    public const int MaxSearchLength = 100;

    public Task<PagedResult<SongDto>> HandleAsync(ListSongsQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var search = query.Search?.Trim();
        if (string.IsNullOrEmpty(search)) search = null;
        else if (search.Length > MaxSearchLength) search = search[..MaxSearchLength];

        return songs.ListAsync(page, pageSize, search, query.Status, ct);
    }
}