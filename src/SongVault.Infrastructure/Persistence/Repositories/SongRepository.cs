using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Common;
using SongVault.Application.Files;
using SongVault.Application.Songs;
using SongVault.Application.Versions;
using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// TOUTES les lectures partent de OwnedSongs : un utilisateur ne voit jamais les données d'un autre.
/// Une ressource d'autrui est donc « introuvable » (404), sans révéler son existence.
/// </summary>
internal sealed class SongRepository(SongVaultDbContext db, ICurrentUser currentUser) : ISongRepository
{
    private IQueryable<Song> OwnedSongs => db.Songs.Where(s => s.OwnerId == currentUser.UserId);
    private IQueryable<SongVersion> OwnedVersions => OwnedSongs.SelectMany(s => s.Versions);

    // ---------- Écriture ----------
    public void Add(Song song) => db.Songs.Add(song);
    public void Remove(Song song) => db.Songs.Remove(song);

    // ---------- Morceaux ----------
    public Task<Song?> GetByIdAsync(Guid id, CancellationToken ct)
        => OwnedSongs.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<bool> ExistsAsync(Guid songId, CancellationToken ct)
        => OwnedSongs.AnyAsync(s => s.Id == songId, ct);

    public async Task<PagedResult<SongDto>> ListAsync(
        int page, int pageSize, string? search, SongVersionStatus? status, CancellationToken ct)
    {
        var query = OwnedSongs.AsNoTracking();

        if (search is not null)
            query = query.Where(s => s.Title.Contains(search) || (s.Artist != null && s.Artist.Contains(search)));

        if (status is not null)
            query = query.Where(s => s.Versions
                .OrderByDescending(v => v.Number)
                .Select(v => (SongVersionStatus?)v.Status)
                .FirstOrDefault() == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.Title).ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new SongDto(s.Id, s.Title, s.Artist, s.Description, s.CreatedAt, s.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResult<SongDto>(items, page, pageSize, total);
    }

    // ---------- Versions ----------
    public Task<Song?> GetWithVersionAsync(Guid songId, Guid versionId, CancellationToken ct)
        => OwnedSongs
            .Include(s => s.Versions.Where(v => v.Id == versionId))
                .ThenInclude(v => v.Files)
            .FirstOrDefaultAsync(s => s.Id == songId, ct);

    public async Task<IReadOnlyList<SongVersionSummaryDto>> ListVersionsAsync(Guid songId, CancellationToken ct)
        => await OwnedVersions.AsNoTracking()
            .Where(v => v.SongId == songId)
            .OrderBy(v => v.Number)
            .Select(v => new SongVersionSummaryDto(v.Id, v.Number, v.Title, v.Status, v.CreatedAt))
            .ToListAsync(ct);

    public Task<SongVersionDto?> GetVersionAsync(Guid songId, Guid versionId, CancellationToken ct)
        => OwnedVersions.AsNoTracking()
            .Where(v => v.SongId == songId && v.Id == versionId)
            .Select(v => new SongVersionDto(
                v.Id, v.SongId, v.Number, v.Title, v.Status, v.Notes, v.Lyrics,
                v.Bpm, v.Key == null ? null : v.Key.Value,
                v.CreatedAt, v.UpdatedAt,
                v.Files.OrderBy(f => f.UploadedAt)
                       .Select(f => new SongFileDto(f.Id, f.OriginalFileName, f.ContentType, f.SizeBytes, f.FileType, f.UploadedAt))
                       .ToList()))
            .FirstOrDefaultAsync(ct);

    // ---------- Fichiers ----------
    public Task<StoredFileInfo?> GetFileAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
        => OwnedVersions.AsNoTracking()
            .Where(v => v.SongId == songId && v.Id == versionId)
            .SelectMany(v => v.Files)
            .Where(f => f.Id == fileId)
            .Select(f => new StoredFileInfo(f.StorageKey, f.ContentType, f.OriginalFileName))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<string>> ListStorageKeysAsync(Guid songId, CancellationToken ct)
        => await OwnedVersions.AsNoTracking()
            .Where(v => v.SongId == songId)
            .SelectMany(v => v.Files)
            .Select(f => f.StorageKey)
            .ToListAsync(ct);
}