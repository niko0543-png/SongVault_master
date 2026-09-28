using SongVault.Application.Common;
using SongVault.Application.Songs;
using SongVault.Application.Versions;
using SongVault.Domain.Songs;

namespace SongVault.Application.Abstractions;

public interface ISongRepository
{
    void Add(Song song);
    void Remove(Song song);
    Task<Song?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<PagedResult<SongDto>> ListAsync(int page, int pageSize, CancellationToken ct);
    Task<Song?> GetWithVersionAsync(Guid songId, Guid versionId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid songId, CancellationToken ct);
    Task<IReadOnlyList<SongVersionSummaryDto>> ListVersionsAsync(Guid songId, CancellationToken ct);
    Task<SongVersionDto?> GetVersionAsync(Guid songId, Guid versionId, CancellationToken ct);
}