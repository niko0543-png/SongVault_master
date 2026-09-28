using SongVault.Application.Abstractions;
using SongVault.Application.Common;

namespace SongVault.Application.Songs.ListSongs;

public sealed class ListSongsHandler(ISongRepository songs)
{
    public const int MaxPageSize = 50;

    public Task<PagedResult<SongDto>> HandleAsync(ListSongsQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        return songs.ListAsync(page, pageSize, ct);
    }
}