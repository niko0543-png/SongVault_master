using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Files;
using SongVault.Domain.Activity;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence.Repositories;

internal sealed class FileScanRepository(SongVaultDbContext db) : IFileScanRepository
{
    public void Add(FileScan scan) => db.FileScans.Add(scan);

    public Task<FileScan?> GetAsync(Guid songFileId, CancellationToken ct)
        => db.FileScans.FirstOrDefaultAsync(s => s.SongFileId == songFileId, ct);

    public async Task<IReadOnlyList<FileScan>> ListNotScannedAsync(int take, CancellationToken ct)
        => await db.FileScans
            .Where(s => s.Status == FileScanStatus.NotScanned)
            .OrderBy(s => s.RequestedAt)
            .Take(take)
            .ToListAsync(ct);

    // SongFile n'a pas de filtre de groupe (seul Song en a un) : lisible hors requête HTTP
    public async Task<FileScanTarget?> GetTargetAsync(Guid songFileId, CancellationToken ct)
    {
        // #19 : avec le groupe et le nom du fichier, pour le journal d'audit s'il est bloqué
        var row = await (
                from f in db.Set<SongFile>().AsNoTracking()
                join v in db.Set<SongVersion>() on f.SongVersionId equals v.Id
                join s in db.Songs.IgnoreQueryFilters([SongVaultDbContext.BandFilter]) on v.SongId equals s.Id
                where f.Id == songFileId
                select new { f.StorageKey, f.FileType, s.BandId, s.Title, v.Number, f.OriginalFileName })
            .FirstOrDefaultAsync(ct);
        return row is null
            ? null
            : new FileScanTarget(row.StorageKey, row.FileType, row.BandId,
                DeleteSongFileHandler.FileSubject(row.Title, row.Number, row.OriginalFileName));
    }

    public Task<InfectedFileNotice?> GetInfectedNoticeAsync(Guid songFileId, CancellationToken ct)
        => (from scan in db.FileScans.AsNoTracking()
            join f in db.Set<SongFile>() on scan.SongFileId equals f.Id
            join v in db.Set<SongVersion>() on f.SongVersionId equals v.Id
            join s in db.Songs.IgnoreQueryFilters([SongVaultDbContext.BandFilter]) on v.SongId equals s.Id
            join b in db.Bands on s.BandId equals b.Id
            // L'auteur de l'envoi : celui de l'événement « fichier ajouté » du fil (#11)
            join e in db.ActivityEvents on (Guid?)f.Id equals e.EntityId
            join u in db.Users on e.ActorId equals u.Id
            where scan.SongFileId == songFileId && scan.Status == FileScanStatus.Infected
                  && e.Type == ActivityType.FileAdded && u.Email != null
            select new InfectedFileNotice(u.Email!, b.Id, b.Name, s.Id, s.Title, v.Id, v.Number,
                f.OriginalFileName, scan.Threat ?? "Menace inconnue"))
            .FirstOrDefaultAsync(ct);
}