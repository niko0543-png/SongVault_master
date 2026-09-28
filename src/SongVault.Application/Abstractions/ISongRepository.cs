using SongVault.Application.Common;
using SongVault.Application.Songs;
using SongVault.Domain.Songs;

namespace SongVault.Application.Abstractions;

public interface ISongRepository
{
    void Add(Song song);
    void Remove(Song song);
    Task<Song?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<PagedResult<SongDto>> ListAsync(int page, int pageSize, CancellationToken ct); 
}