using SongVault.Application.Songs;
using SongVault.Domain.Songs;

namespace SongVault.Application.Abstractions;

public interface ISongRepository
{
    void Add(Song song);
    void Remove(Song song);
    Task<Song?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SongDto>> ListAsync(CancellationToken ct);   // lecture : projection directe
}