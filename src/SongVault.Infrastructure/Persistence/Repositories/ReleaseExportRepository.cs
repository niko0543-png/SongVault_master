using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Releases;
using SongVault.Application.Releases.Exports;
using SongVault.Domain.Files;
using SongVault.Domain.Releases;
using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// La tâche de fond n'a pas de groupe actif : le filtre du groupe est retiré et remplacé par le groupe passé en
/// paramètre. La corbeille (#20) reste filtrée : un fichier à la corbeille n'entre pas dans l'archive.
/// </summary>
internal sealed class ReleaseExportRepository(SongVaultDbContext db) : IReleaseExportRepository
{
    private IQueryable<Release> BandReleases(Guid bandId)
        => db.Releases.IgnoreQueryFilters([SongVaultDbContext.BandFilter]).Where(r => r.BandId == bandId);

    private IQueryable<Song> BandSongs(Guid bandId)
        => db.Songs.IgnoreQueryFilters([SongVaultDbContext.BandFilter]).Where(s => s.BandId == bandId);

    public void Add(ReleaseExport export) => db.ReleaseExports.Add(export);

    public void Remove(ReleaseExport export) => db.ReleaseExports.Remove(export);

    public Task<ReleaseExport?> GetAsync(Guid exportId, CancellationToken ct)
        => db.ReleaseExports.FirstOrDefaultAsync(e => e.Id == exportId, ct);

    public Task<ReleaseExport?> GetLatestAsync(Guid bandId, Guid releaseId, CancellationToken ct)
        => db.ReleaseExports
            .Where(e => e.BandId == bandId && e.ReleaseId == releaseId)
            .OrderByDescending(e => e.RequestedAt)
            .FirstOrDefaultAsync(ct);

    public Task<string?> GetReleaseTitleAsync(Guid bandId, Guid releaseId, CancellationToken ct)
        => BandReleases(bandId).Where(r => r.Id == releaseId).Select(r => r.Title).FirstOrDefaultAsync(ct);

    public async Task<ReleaseArchiveSource?> ReadSourceAsync(Guid bandId, Guid releaseId, CancellationToken ct)
    {
        var release = await BandReleases(bandId).AsNoTracking()
            .Include(r => r.Tracks)
            .FirstOrDefaultAsync(r => r.Id == releaseId, ct);
        if (release is null) return null;

        var songIds = release.Tracks.Select(t => t.SongId).ToList();
        var versionIds = release.Tracks.Select(t => t.VersionId).ToList();
        var songs = await BandSongs(bandId).AsNoTracking()
            .Include(s => s.Credits)
            .Include(s => s.Versions)
            .AsSplitQuery()
            .Where(s => songIds.Contains(s.Id))
            .ToListAsync(ct);
        var files = await db.Set<SongFile>().AsNoTracking()
            .Where(f => versionIds.Contains(f.SongVersionId) && f.FileType == SongFileType.Audio)
            .OrderBy(f => f.UploadedAt)
            .Select(f => new ReleaseArchiveFile(f.SongVersionId, f.OriginalFileName, f.StorageKey, f.StemName, f.StemPosition,
                db.FileScans.Where(s => s.SongFileId == f.Id).Select(s => (FileScanStatus?)s.Status).FirstOrDefault()))
            .ToListAsync(ct);

        // Même forme que GET …/releases/{id} : CreditsCsv.ForRelease la lit telle quelle
        var byId = songs.ToDictionary(s => s.Id);
        var detail = new ReleaseDto(release.Id, release.Title, release.Type, release.ReleaseDate,
            release.CoverStorageKey is not null, release.CreatedAt, release.UpdatedAt,
            [.. release.Tracks.Select(t =>
            {
                var song = byId.GetValueOrDefault(t.SongId);
                return new ReleaseTrackDto(t.TrackNumber, t.SongId, song?.Title ?? "", t.VersionId,
                    [.. (song?.Versions ?? []).OrderBy(v => v.Number)
                        .Select(v => new ReleaseVersionOptionDto(v.Id, v.Number, v.Title, v.Status))]);
            })]);
        return new ReleaseArchiveSource(detail, release.CoverStorageKey, release.CoverContentType, songs, files);
    }
}