using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Files;
using SongVault.Application.Trash;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;
using SongVault.Domain.Trash;

namespace SongVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// Corbeille (#20, ADR 0027). Les lectures du groupe actif ôtent seulement le filtre SoftDelete ; la purge, qui tourne
/// hors requête HTTP, ôte aussi celui du groupe.
/// </summary>
internal sealed class TrashRepository(SongVaultDbContext db) : ITrashRepository
{
    private static readonly string[] SoftDelete = [SongVaultDbContext.SoftDeleteFilter];
    private static readonly string[] AllFilters = [SongVaultDbContext.BandFilter, SongVaultDbContext.SoftDeleteFilter];

    /// <summary>Morceaux du groupe actif (filtre Band), corbeille comprise.</summary>
    private IQueryable<Song> BandSongs => db.Songs.IgnoreQueryFilters(SoftDelete);

    public async Task<IReadOnlyList<TrashItemDto>> ListAsync(CancellationToken ct)
    {
        var songs = await (
                from s in BandSongs.AsNoTracking()
                where s.DeletedAt != null
                select new TrashItemDto(TrashItemKind.Song, s.Id, s.Id, s.Title, null, null, null,
                    s.Versions.SelectMany(v => v.Files).Sum(f => (long?)f.SizeBytes) ?? 0L,
                    s.DeletedAt!.Value, s.DeletedById,
                    db.Users.Where(u => u.Id == s.DeletedById).Select(u => u.Email).FirstOrDefault()))
            .ToListAsync(ct);

        // Une version ou un fichier n'apparaît seul que si ce qui le contient est en place
        var versions = await (
                from s in BandSongs.AsNoTracking()
                from v in s.Versions
                where s.DeletedAt == null && v.DeletedAt != null
                select new TrashItemDto(TrashItemKind.Version, v.Id, s.Id, s.Title, v.Number, v.Title, null,
                    v.Files.Sum(f => (long?)f.SizeBytes) ?? 0L,
                    v.DeletedAt!.Value, v.DeletedById,
                    db.Users.Where(u => u.Id == v.DeletedById).Select(u => u.Email).FirstOrDefault()))
            .ToListAsync(ct);

        var files = await (
                from s in BandSongs.AsNoTracking()
                from v in s.Versions
                from f in v.Files
                where s.DeletedAt == null && v.DeletedAt == null && f.DeletedAt != null
                select new TrashItemDto(TrashItemKind.File, f.Id, s.Id, s.Title, v.Number, v.Title, f.OriginalFileName,
                    f.SizeBytes, f.DeletedAt!.Value, f.DeletedById,
                    db.Users.Where(u => u.Id == f.DeletedById).Select(u => u.Email).FirstOrDefault()))
            .ToListAsync(ct);

        return [.. songs.Concat(versions).Concat(files).OrderByDescending(i => i.DeletedAt).ThenBy(i => i.Id)];
    }

    public async Task<Song?> GetSongAsync(TrashItemKind kind, Guid itemId, CancellationToken ct)
    {
        var songId = await SongIdOf(BandSongs, kind, itemId).FirstOrDefaultAsync(ct);
        return songId is null
            ? null
            : await BandSongs
                .Include(s => s.Versions).ThenInclude(v => v.Files)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.Id == songId, ct);
    }

    public async Task<IReadOnlyList<TrashEntry>> ListExpiredAsync(DateTimeOffset deletedUpTo, CancellationToken ct)
    {
        var all = db.Songs.IgnoreQueryFilters(AllFilters).AsNoTracking();
        var songs = await all.Where(s => s.DeletedAt <= deletedUpTo)
            .Select(s => new TrashEntry(TrashItemKind.Song, s.Id)).ToListAsync(ct);
        var versions = await all.SelectMany(s => s.Versions).Where(v => v.DeletedAt <= deletedUpTo)
            .Select(v => new TrashEntry(TrashItemKind.Version, v.Id)).ToListAsync(ct);
        var files = await all.SelectMany(s => s.Versions).SelectMany(v => v.Files).Where(f => f.DeletedAt <= deletedUpTo)
            .Select(f => new TrashEntry(TrashItemKind.File, f.Id)).ToListAsync(ct);
        return [.. songs, .. versions, .. files];       // morceaux d'abord : leurs versions partent avec eux
    }

    public async Task<PurgeTarget?> GetPurgeTargetAsync(TrashItemKind kind, Guid itemId, CancellationToken ct)
    {
        var all = db.Songs.IgnoreQueryFilters(AllFilters).AsNoTracking();
        var item = kind switch
        {
            TrashItemKind.Song => await all.Where(s => s.Id == itemId && s.DeletedAt != null)
                .Select(s => new { s.BandId, Label = s.Title }).FirstOrDefaultAsync(ct),
            TrashItemKind.Version => await all.SelectMany(s => s.Versions, (s, v) => new { s, v })
                .Where(x => x.v.Id == itemId && x.v.DeletedAt != null)
                .Select(x => new { x.s.BandId, Label = x.s.Title + " — v" + x.v.Number }).FirstOrDefaultAsync(ct),
            _ => await all.SelectMany(s => s.Versions, (s, v) => new { s, v })
                .SelectMany(x => x.v.Files, (x, f) => new { x.s, x.v, f })
                .Where(x => x.f.Id == itemId && x.f.DeletedAt != null)
                .Select(x => new { x.s.BandId, Label = x.s.Title + " — v" + x.v.Number + " — " + x.f.OriginalFileName })
                .FirstOrDefaultAsync(ct),
        };
        if (item is null) return null;

        var files = FilesOf(all, kind, itemId);
        var keys = await files.Select(f => f.StorageKey).ToListAsync(ct);
        keys.AddRange(await db.AudioPreviews.AsNoTracking()                // #15 : et leurs MP3 d'aperçu
            .Where(p => p.PreviewStorageKey != null && files.Any(f => f.Id == p.SongFileId))
            .Select(p => p.PreviewStorageKey!)
            .ToListAsync(ct));
        return new PurgeTarget(item.BandId, item.Label, keys);
    }

    public Task DeleteAsync(TrashItemKind kind, Guid itemId, CancellationToken ct)
    {
        var all = db.Songs.IgnoreQueryFilters(AllFilters);
        return kind switch
        {
            TrashItemKind.Song => all.Where(s => s.Id == itemId && s.DeletedAt != null).ExecuteDeleteAsync(ct),
            TrashItemKind.Version => db.Set<SongVersion>().IgnoreQueryFilters(AllFilters)
                .Where(v => v.Id == itemId && v.DeletedAt != null).ExecuteDeleteAsync(ct),
            _ => db.Set<SongFile>().IgnoreQueryFilters(AllFilters)
                .Where(f => f.Id == itemId && f.DeletedAt != null).ExecuteDeleteAsync(ct),
        };
    }

    private static IQueryable<Guid?> SongIdOf(IQueryable<Song> songs, TrashItemKind kind, Guid itemId) => kind switch
    {
        TrashItemKind.Song => songs.Where(s => s.Id == itemId).Select(s => (Guid?)s.Id),
        TrashItemKind.Version => songs.SelectMany(s => s.Versions).Where(v => v.Id == itemId).Select(v => (Guid?)v.SongId),
        _ => songs.SelectMany(s => s.Versions, (s, v) => new { s.Id, v.Files })
            .Where(x => x.Files.Any(f => f.Id == itemId)).Select(x => (Guid?)x.Id),
    };

    private static IQueryable<SongFile> FilesOf(IQueryable<Song> songs, TrashItemKind kind, Guid itemId) => kind switch
    {
        TrashItemKind.Song => songs.Where(s => s.Id == itemId).SelectMany(s => s.Versions).SelectMany(v => v.Files),
        TrashItemKind.Version => songs.SelectMany(s => s.Versions).Where(v => v.Id == itemId).SelectMany(v => v.Files),
        _ => songs.SelectMany(s => s.Versions).SelectMany(v => v.Files).Where(f => f.Id == itemId),
    };
}