using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Songs;
using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence.Repositories;

internal sealed class SongRepository(SongVaultDbContext db) : ISongRepository
{
    public void Add(Song song) => db.Songs.Add(song);

    public void Remove(Song song) => db.Songs.Remove(song);

    public Task<Song?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.Songs.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SongDto>> ListAsync(CancellationToken ct)
        => await db.Songs
            .AsNoTracking()
            .OrderBy(s => s.Title)
            .Select(s => new SongDto(s.Id, s.Title, s.Artist, s.Description, s.CreatedAt, s.UpdatedAt))
            .ToListAsync(ct);
}