using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Files.Splits;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// Le filtre du groupe actif est ôté, et remplacé par le groupe passé en paramètre : la tâche de fond n'en a pas.
/// Le filtre de la corbeille reste.
/// </summary>
internal sealed class AudioSplitRepository(SongVaultDbContext db) : IAudioSplitRepository
{
    private IQueryable<Song> SongsOf(Guid bandId)
        => db.Songs.IgnoreQueryFilters([SongVaultDbContext.BandFilter]).Where(s => s.BandId == bandId);

    public void Add(AudioSplit split) => db.AudioSplits.Add(split);

    public Task<AudioSplit?> GetAsync(Guid splitId, CancellationToken ct)
        => db.AudioSplits.FirstOrDefaultAsync(s => s.Id == splitId, ct);

    public Task<AudioSplit?> GetLatestAsync(Guid bandId, Guid sourceFileId, CancellationToken ct)
        => db.AudioSplits.AsNoTracking()
            .Where(s => s.BandId == bandId && s.SourceFileId == sourceFileId)
            .OrderByDescending(s => s.RequestedAt)
            .FirstOrDefaultAsync(ct);

    public Task<AudioSplitSource?> GetSourceAsync(Guid bandId, Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
        => (from s in SongsOf(bandId).AsNoTracking()
            from v in s.Versions
            from f in v.Files
            where s.Id == songId && v.Id == versionId && f.Id == fileId
            let preview = db.AudioPreviews.FirstOrDefault(p => p.SongFileId == f.Id)
            select new AudioSplitSource(f.Id, f.OriginalFileName, f.SizeBytes, f.FileType,
                db.FileScans.Where(x => x.SongFileId == f.Id).Select(x => (FileScanStatus?)x.Status).FirstOrDefault(),
                preview == null ? null : preview.Status,
                preview == null ? null : preview.DurationMs))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlySet<Guid>> ListLiveSongIdsAsync(Guid bandId, IReadOnlyCollection<Guid> songIds, CancellationToken ct)
    {
        if (songIds.Count == 0) return new HashSet<Guid>();
        return (await SongsOf(bandId).Where(s => songIds.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct)).ToHashSet();
    }

    public Task<Song?> GetSongAsync(Guid bandId, Guid songId, CancellationToken ct)
        => SongsOf(bandId).FirstOrDefaultAsync(s => s.Id == songId, ct);

    public void AddSong(Song song) => db.Songs.Add(song);
}
