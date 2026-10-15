using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Domain.Files;

namespace SongVault.Infrastructure.Persistence.Repositories;

internal sealed class AudioPreviewRepository(SongVaultDbContext db) : IAudioPreviewRepository
{
    public void Add(AudioPreview preview) => db.AudioPreviews.Add(preview);

    public Task<AudioPreview?> GetAsync(Guid songFileId, CancellationToken ct)
        => db.AudioPreviews.FirstOrDefaultAsync(p => p.SongFileId == songFileId, ct);

    // SongFile n'a pas de filtre de groupe (seul Song en a un) : lisible hors requête HTTP
    public Task<string?> GetSourceKeyAsync(Guid songFileId, CancellationToken ct)
        => db.Set<SongFile>().AsNoTracking()
            .Where(f => f.Id == songFileId)
            .Select(f => f.StorageKey)
            .FirstOrDefaultAsync(ct);
}