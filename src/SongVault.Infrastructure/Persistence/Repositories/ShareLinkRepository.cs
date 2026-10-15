using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Files;
using SongVault.Application.Sharing;
using SongVault.Domain.Files;
using SongVault.Domain.Releases;
using SongVault.Domain.Sharing;
using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// Les lectures publiques n'ont pas de groupe actif : elles ôtent le filtre de groupe et partent toujours du lien
/// (sa cible et son BandId). Un identifiant de fichier changé à la main ne sort jamais de la cible.
/// </summary>
internal sealed class ShareLinkRepository(SongVaultDbContext db) : IShareLinkRepository
{
    private IQueryable<Song> AllSongs => db.Songs.IgnoreQueryFilters([SongVaultDbContext.BandFilter]);
    private IQueryable<Release> AllReleases => db.Releases.IgnoreQueryFilters([SongVaultDbContext.BandFilter]);

    public void Add(ShareLink link) => db.ShareLinks.Add(link);

    public void Remove(ShareLink link) => db.ShareLinks.Remove(link);

    public void AddAccess(ShareLinkAccess access) => db.ShareLinkAccesses.Add(access);

    public Task<ShareLink?> GetByTokenHashAsync(byte[] tokenHash, CancellationToken ct)
        => db.ShareLinks.AsNoTracking().FirstOrDefaultAsync(l => l.TokenHash == tokenHash, ct);

    public Task<ShareLink?> GetInBandAsync(Guid bandId, Guid linkId, CancellationToken ct)
        => db.ShareLinks.FirstOrDefaultAsync(l => l.Id == linkId && l.BandId == bandId, ct);

    public Task<bool> TargetExistsAsync(Guid bandId, ShareTargetType type, Guid targetId, CancellationToken ct)
        => type == ShareTargetType.Version
            ? AllSongs.Where(s => s.BandId == bandId).SelectMany(s => s.Versions).AnyAsync(v => v.Id == targetId, ct)
            : AllReleases.AnyAsync(r => r.BandId == bandId && r.Id == targetId, ct);

    public async Task<IReadOnlyList<ShareLinkDto>> ListAsync(
        Guid bandId, ShareTargetType type, Guid targetId, DateTimeOffset now, CancellationToken ct)
    {
        var links = db.ShareLinks.AsNoTracking().Where(l => l.BandId == bandId && l.TargetType == type);
        links = type == ShareTargetType.Version
            ? links.Where(l => l.VersionId == targetId)
            : links.Where(l => l.ReleaseId == targetId);

        var rows = await links
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new
            {
                Link = l,
                CreatedByEmail = db.Users.Where(u => u.Id == l.CreatedById).Select(u => u.Email).FirstOrDefault(),
                OpenCount = db.ShareLinkAccesses.Count(a => a.ShareLinkId == l.Id && a.Kind == ShareAccessKind.Opened),
                LastOpenedAt = db.ShareLinkAccesses
                    .Where(a => a.ShareLinkId == l.Id && a.Kind == ShareAccessKind.Opened)
                    .Max(a => (DateTimeOffset?)a.At),
            })
            .ToListAsync(ct);

        return [.. rows.Select(r => new ShareLinkDto(
            r.Link.Id, r.Link.TargetType, targetId, r.Link.Label, r.Link.HasPassword, r.Link.StatusAt(now),
            r.Link.CreatedAt, r.Link.ExpiresAt, r.Link.RevokedAt, r.CreatedByEmail, r.OpenCount, r.LastOpenedAt))];
    }

    public async Task<IReadOnlyList<ShareLinkAccessDto>> ListAccessesAsync(Guid linkId, int limit, CancellationToken ct)
    {
        var rows = await db.ShareLinkAccesses.AsNoTracking()
            .Where(a => a.ShareLinkId == linkId)
            .OrderByDescending(a => a.Id)
            .Take(limit)
            .Select(a => new
            {
                a.At,
                a.Kind,
                // Fichier supprimé depuis : la ligne reste, sans nom
                FileName = db.Set<SongFile>().Where(f => f.Id == a.FileId).Select(f => f.OriginalFileName).FirstOrDefault(),
                a.IpHash,
                a.UserAgent,
            })
            .ToListAsync(ct);

        return [.. rows.Select(r => new ShareLinkAccessDto(r.At, r.Kind, r.FileName,
            r.IpHash is null ? null : Convert.ToHexStringLower(r.IpHash, 0, 4), r.UserAgent))];
    }

    public async Task<SharedContentDto?> ReadContentAsync(ShareLink link, CancellationToken ct)
    {
        var bandName = await db.Bands.AsNoTracking()
            .Where(b => b.Id == link.BandId)
            .Select(b => b.Name)
            .FirstOrDefaultAsync(ct);
        if (bandName is null) return null;

        if (link.TargetType == ShareTargetType.Version)
        {
            var version = await (
                    from v in db.Set<SongVersion>().AsNoTracking()
                    join s in AllSongs on v.SongId equals s.Id
                    where v.Id == link.VersionId && s.BandId == link.BandId
                    select new { v.Id, s.Title, s.Artist, v.Number, VersionTitle = v.Title })
                .FirstOrDefaultAsync(ct);
            if (version is null) return null;

            var files = await AudioFilesAsync([version.Id], ct);
            return new SharedContentDto(ShareTargetType.Version, bandName, version.Title, null, null, false, link.ExpiresAt,
                [new SharedTrackDto(1, version.Title, version.Artist, version.Number, version.VersionTitle, [.. files[version.Id]])]);
        }

        var release = await AllReleases.AsNoTracking()
            .Where(r => r.Id == link.ReleaseId && r.BandId == link.BandId)
            .Select(r => new { r.Title, r.Type, r.ReleaseDate, HasCover = r.CoverStorageKey != null })
            .FirstOrDefaultAsync(ct);
        if (release is null) return null;

        var tracks = await (
                from t in db.Set<ReleaseTrack>().AsNoTracking()
                join v in db.Set<SongVersion>() on t.VersionId equals v.Id
                join s in AllSongs on v.SongId equals s.Id
                where t.ReleaseId == link.ReleaseId && s.BandId == link.BandId
                orderby t.TrackNumber
                select new { t.TrackNumber, s.Title, s.Artist, VersionId = v.Id, v.Number, VersionTitle = v.Title })
            .ToListAsync(ct);
        var trackFiles = await AudioFilesAsync([.. tracks.Select(t => t.VersionId)], ct);

        return new SharedContentDto(ShareTargetType.Release, bandName, release.Title, release.Type, release.ReleaseDate,
            release.HasCover, link.ExpiresAt,
            [.. tracks.Select(t => new SharedTrackDto(
                t.TrackNumber, t.Title, t.Artist, t.Number, t.VersionTitle, [.. trackFiles[t.VersionId]]))]);
    }

    public Task<StoredFileInfo?> GetSharedFileAsync(ShareLink link, Guid fileId, CancellationToken ct)
    {
        var versionIds = SharedVersionIds(link);
        return db.Set<SongFile>().AsNoTracking()
            .Where(f => f.Id == fileId && f.FileType == SongFileType.Audio && versionIds.Contains(f.SongVersionId))
            .Select(f => new StoredFileInfo(f.StorageKey, f.ContentType, f.OriginalFileName))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<StoredFileInfo?> GetSharedCoverAsync(ShareLink link, CancellationToken ct)
    {
        if (link.TargetType != ShareTargetType.Release) return null;
        var cover = await AllReleases.AsNoTracking()
            .Where(r => r.Id == link.ReleaseId && r.BandId == link.BandId && r.CoverStorageKey != null)
            .Select(r => new { Key = r.CoverStorageKey!, ContentType = r.CoverContentType! })
            .FirstOrDefaultAsync(ct);
        return cover is null ? null : new StoredFileInfo(cover.Key, cover.ContentType, $"pochette{Path.GetExtension(cover.Key)}");
    }

    // Versions que le lien laisse écouter : la sienne, ou celles des pistes de la sortie, toujours dans son groupe
    private IQueryable<Guid> SharedVersionIds(ShareLink link)
        => link.TargetType == ShareTargetType.Version
            ? AllSongs.Where(s => s.BandId == link.BandId).SelectMany(s => s.Versions)
                .Where(v => v.Id == link.VersionId).Select(v => v.Id)
            : from t in db.Set<ReleaseTrack>()
              join v in db.Set<SongVersion>() on t.VersionId equals v.Id
              join s in AllSongs on v.SongId equals s.Id
              where t.ReleaseId == link.ReleaseId && s.BandId == link.BandId
              select v.Id;

    // Fichiers audio de ces versions, dans l'ordre de dépôt. Tri avant la projection : EF ne trie pas un record construit.
    private async Task<ILookup<Guid, SharedFileDto>> AudioFilesAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken ct)
        => (await db.Set<SongFile>().AsNoTracking()
                .Where(f => versionIds.Contains(f.SongVersionId) && f.FileType == SongFileType.Audio)
                .OrderBy(f => f.UploadedAt)
                .Select(f => new { f.SongVersionId, File = new SharedFileDto(f.Id, f.OriginalFileName, f.ContentType, f.SizeBytes) })
                .ToListAsync(ct))
            .ToLookup(f => f.SongVersionId, f => f.File);
}