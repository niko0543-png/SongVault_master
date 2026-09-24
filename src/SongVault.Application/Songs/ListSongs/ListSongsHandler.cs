using SongVault.Application._Temporary;

namespace SongVault.Application.Songs.ListSongs;

public sealed class ListSongsHandler(InMemorySongStore store)
{
    public Task<IReadOnlyList<SongDto>> HandleAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<SongDto> result = [.. store.List().Select(SongDto.From)];
        return Task.FromResult(result);
    }
}