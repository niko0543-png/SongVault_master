using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Common;
using SongVault.Application.Songs;
using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence.Repositories;

internal sealed class SongRepository(SongVaultDbContext db) : ISongRepository
{
    public void Add(Song song) => db.Songs.Add(song);

    public void Remove(Song song) => db.Songs.Remove(song);

    public Task<Song?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.Songs.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<PagedResult<SongDto>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = db.Songs.AsNoTracking();

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.Title).ThenBy(s => s.Id)             // ordre déterministe
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SongDto(s.Id, s.Title, s.Artist, s.Description, s.CreatedAt, s.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResult<SongDto>(items, page, pageSize, total);
    }
}