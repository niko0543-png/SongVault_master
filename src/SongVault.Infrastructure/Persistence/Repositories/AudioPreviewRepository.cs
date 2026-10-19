using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Domain.Files;

namespace SongVault.Infrastructure.Persistence.Repositories;

internal sealed class AudioPreviewRepository(SongVaultDbContext db) : IAudioPreviewRepository
{
    public void Add(AudioPreview preview) => db.AudioPreviews.Add(preview);

    public Task<AudioPreview?> GetAsync(Guid songFileId, CancellationToken ct)
        => db.AudioPreviews.FirstOrDefaultAsync(p => p.SongFileId == songFileId, ct);

    // SongFile n'a pas de filtre de groupe (seul Song en a un) : lisible hors requête HTTP.
    // #20 : un fichier à la corbeille reçoit quand même son aperçu, prêt s'il est restauré
    public Task<string?> GetSourceKeyAsync(Guid songFileId, CancellationToken ct)
        => db.Set<SongFile>().AsNoTracking().IgnoreQueryFilters([SongVaultDbContext.SoftDeleteFilter])
            .Where(f => f.Id == songFileId
                        && db.FileScans.Any(s => s.SongFileId == f.Id && (s.Status == FileScanStatus.Clean || s.Status == FileScanStatus.NotScanned)))   // #17
            .Select(f => f.StorageKey)
            .FirstOrDefaultAsync(ct);
}