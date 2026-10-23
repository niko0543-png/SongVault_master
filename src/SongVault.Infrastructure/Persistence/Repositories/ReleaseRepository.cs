using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Releases;
using SongVault.Domain.Releases;

namespace SongVault.Infrastructure.Persistence.Repositories;

/// <summary>Toutes les lectures partent de BandReleases : seulement le groupe actif, vérifié par [BandScoped].</summary>
internal sealed class ReleaseRepository(SongVaultDbContext db, IBandContext bandContext) : IReleaseRepository
{
    private IQueryable<Release> BandReleases => db.Releases.Where(r => r.BandId == bandContext.RequiredBandId);

    public void Add(Release release) => db.Releases.Add(release);

    public void Remove(Release release) => db.Releases.Remove(release);

    public Task<Release?> GetAsync(Guid releaseId, CancellationToken ct)
        => BandReleases.Include(r => r.Tracks).FirstOrDefaultAsync(r => r.Id == releaseId, ct);

    public async Task<IReadOnlyList<ReleaseSummaryDto>> ListAsync(CancellationToken ct)
        => await BandReleases.AsNoTracking()
            .OrderBy(r => r.ReleaseDate == null ? 0 : 1)                 // en préparation d'abord
            .ThenByDescending(r => r.ReleaseDate)
            .ThenByDescending(r => r.CreatedAt)
            .Select(r => new ReleaseSummaryDto(
                r.Id, r.Title, r.Type, r.ReleaseDate, r.CoverStorageKey != null, r.Tracks.Count()))
            .ToListAsync(ct);

    public async Task<ReleaseDto?> GetDetailAsync(Guid releaseId, CancellationToken ct)
    {
        var release = await BandReleases.AsNoTracking()
            .Where(r => r.Id == releaseId)
            .Select(r => new
            {
                r.Id, r.Title, r.Type, r.ReleaseDate, HasCover = r.CoverStorageKey != null, r.CreatedAt, r.UpdatedAt,
                r.Label, r.Upc, r.StreamingLinks,
            })
            .FirstOrDefaultAsync(ct);
        if (release is null) return null;

        var tracks = await db.Set<ReleaseTrack>().AsNoTracking()
            .Where(t => t.ReleaseId == releaseId)
            .OrderBy(t => t.TrackNumber)
            .Select(t => new
            {
                t.TrackNumber,
                t.SongId,
                t.VersionId,
                SongTitle = db.Songs.Where(s => s.Id == t.SongId).Select(s => s.Title).FirstOrDefault() ?? "",
                SongReleaseId = db.Songs.Where(s => s.Id == t.SongId).Select(s => s.ReleaseId).FirstOrDefault(),
            })
            .ToListAsync(ct);

        // Toutes les versions des morceaux de la sortie, en une requête : de quoi changer la version choisie
        var songIds = tracks.Select(t => t.SongId).ToList();
        var versions = (await db.Songs.AsNoTracking()
                .Where(s => songIds.Contains(s.Id))
                .SelectMany(s => s.Versions)
                .OrderBy(v => v.Number)
                .Select(v => new { v.SongId, Option = new ReleaseVersionOptionDto(v.Id, v.Number, v.Title, v.Status) })
                .ToListAsync(ct))
            .ToLookup(v => v.SongId, v => v.Option);

        return new ReleaseDto(release.Id, release.Title, release.Type, release.ReleaseDate, release.HasCover,
            release.CreatedAt, release.UpdatedAt,
            [.. tracks.Select(t => new ReleaseTrackDto(t.TrackNumber, t.SongId, t.SongTitle, t.VersionId, [.. versions[t.SongId]],
                t.SongReleaseId == release.Id))],
            release.Label, release.Upc?.Value, release.StreamingLinks);
    }

    public async Task<IReadOnlyList<string>> ListTitlesWithSongAsync(Guid songId, CancellationToken ct)
        => await BandReleases.AsNoTracking()
            .Where(r => r.Tracks.Any(t => t.SongId == songId))
            .OrderBy(r => r.Title)
            .Select(r => r.Title)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> ListTitlesWithVersionAsync(Guid versionId, CancellationToken ct)
        => await BandReleases.AsNoTracking()
            .Where(r => r.Tracks.Any(t => t.VersionId == versionId))
            .OrderBy(r => r.Title)
            .Select(r => r.Title)
            .ToListAsync(ct);
}