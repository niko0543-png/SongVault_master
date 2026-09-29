using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Common;
using SongVault.Application.Files;
using SongVault.Application.Songs;
using SongVault.Application.Versions;
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

    public Task<Song?> GetWithVersionAsync(Guid songId, Guid versionId, CancellationToken ct)
        => db.Songs
            .Include(s => s.Versions.Where(v => v.Id == versionId))
                .ThenInclude(v => v.Files)
            .FirstOrDefaultAsync(s => s.Id == songId, ct);

    public Task<bool> ExistsAsync(Guid songId, CancellationToken ct)
        => db.Songs.AnyAsync(s => s.Id == songId, ct);

    public async Task<IReadOnlyList<SongVersionSummaryDto>> ListVersionsAsync(Guid songId, CancellationToken ct)
        => await db.Set<SongVersion>().AsNoTracking()
            .Where(v => v.SongId == songId)
            .OrderBy(v => v.Number)
            .Select(v => new SongVersionSummaryDto(v.Id, v.Number, v.Title, v.Status, v.CreatedAt))
            .ToListAsync(ct);

    public Task<SongVersionDto?> GetVersionAsync(Guid songId, Guid versionId, CancellationToken ct)
        => db.Set<SongVersion>().AsNoTracking()
            .Where(v => v.SongId == songId && v.Id == versionId)
            .Select(v => new SongVersionDto(v.Id, v.SongId, v.Number, v.Title, v.Status, v.Notes, v.Lyrics,
                v.CreatedAt, v.UpdatedAt,
                v.Files.OrderBy(f => f.UploadedAt)
                       .Select(f => new SongFileDto(f.Id, f.OriginalFileName, f.ContentType, f.SizeBytes, f.FileType, f.UploadedAt))
                       .ToList()))
            .FirstOrDefaultAsync(ct);

    public Task<StoredFileInfo?> GetFileAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
    => db.Set<SongVersion>().AsNoTracking()
        .Where(v => v.SongId == songId && v.Id == versionId)
        .SelectMany(v => v.Files)
        .Where(f => f.Id == fileId)
        .Select(f => new StoredFileInfo(f.StorageKey, f.ContentType, f.OriginalFileName))
        .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<string>> ListStorageKeysAsync(Guid songId, CancellationToken ct)
        => await db.Set<SongVersion>().AsNoTracking()
            .Where(v => v.SongId == songId)
            .SelectMany(v => v.Files)
            .Select(f => f.StorageKey)
            .ToListAsync(ct);
}