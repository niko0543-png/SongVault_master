using SongVault.Domain.Songs;

namespace SongVault.Application.Songs.ListSongs;

public sealed record ListSongsQuery(int Page, int PageSize, string? Search = null, SongVersionStatus? Status = null);