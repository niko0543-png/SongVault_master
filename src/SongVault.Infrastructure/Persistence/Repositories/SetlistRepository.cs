using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Setlists;
using SongVault.Domain.Files;
using SongVault.Domain.Setlists;

namespace SongVault.Infrastructure.Persistence.Repositories;

/// <summary>Toutes les lectures partent de BandSetlists : seulement le groupe actif, vérifié par [BandScoped].</summary>
internal sealed class SetlistRepository(SongVaultDbContext db, IBandContext bandContext) : ISetlistRepository
{
    private IQueryable<Setlist> BandSetlists => db.Setlists.Where(s => s.BandId == bandContext.RequiredBandId);

    public void Add(Setlist setlist) => db.Setlists.Add(setlist);

    public void Remove(Setlist setlist) => db.Setlists.Remove(setlist);

    public Task<Setlist?> GetAsync(Guid setlistId, CancellationToken ct)
        => BandSetlists.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == setlistId, ct);

    public async Task<IReadOnlyList<SetlistSummaryDto>> ListAsync(CancellationToken ct)
        => await BandSetlists.AsNoTracking()
            .OrderBy(s => s.Date == null ? 0 : 1)                         // setlists de travail d'abord
            .ThenByDescending(s => s.Date)
            .ThenByDescending(s => s.CreatedAt)
            .Select(s => new SetlistSummaryDto(s.Id, s.Name, s.Date, s.Venue, s.Items.Count()))
            .ToListAsync(ct);

    public async Task<SetlistDto?> GetDetailAsync(Guid setlistId, CancellationToken ct)
    {
        var setlist = await BandSetlists.AsNoTracking()
            .Where(s => s.Id == setlistId)
            .Select(s => new { s.Id, s.Name, s.Date, s.Venue, s.CreatedAt, s.UpdatedAt })
            .FirstOrDefaultAsync(ct);
        if (setlist is null) return null;

        var items = await db.Set<SetlistItem>().AsNoTracking()
            .Where(i => i.SetlistId == setlistId)
            .OrderBy(i => i.Position)
            .Select(i => new
            {
                i.Position,
                i.SongId,
                i.VersionId,
                i.Notes,
                i.DurationMs,
                SongTitle = db.Songs.Where(s => s.Id == i.SongId).Select(s => s.Title).FirstOrDefault() ?? "",
            })
            .ToListAsync(ct);

        // Toutes les versions des morceaux de la setlist, en une requête (hors corbeille, par le filtre SoftDelete)
        var songIds = items.Select(i => i.SongId).ToList();
        var versions = (await db.Songs.AsNoTracking()
                .Where(s => songIds.Contains(s.Id))
                .SelectMany(s => s.Versions)
                .OrderBy(v => v.Number)
                .Select(v => new { v.Id, v.SongId, v.Number, v.Title, v.Bpm, v.Key })
                .ToListAsync(ct))
            .ToLookup(v => v.SongId);

        // Version jouée : celle choisie, sinon la dernière du morceau
        var played = items.ToDictionary(i => i.SongId,
            i => versions[i.SongId].FirstOrDefault(v => v.Id == i.VersionId) ?? versions[i.SongId].LastOrDefault());

        // Durée (#15) : le dernier fichier audio envoyé sur la version jouée dont l'aperçu est prêt
        var playedIds = played.Values.Where(v => v is not null).Select(v => v!.Id).ToList();
        var durations = (await db.Songs.AsNoTracking()
                .SelectMany(s => s.Versions)
                .Where(v => playedIds.Contains(v.Id))
                .SelectMany(v => v.Files)
                .Where(f => f.FileType == SongFileType.Audio)
                .SelectMany(f => db.AudioPreviews
                    .Where(p => p.SongFileId == f.Id && p.Status == AudioPreviewStatus.Ready && p.DurationMs != null)
                    .Select(p => new { f.SongVersionId, f.UploadedAt, p.DurationMs }))
                .ToListAsync(ct))
            .GroupBy(d => d.SongVersionId)
            .ToDictionary(g => g.Key, g => g.MaxBy(d => d.UploadedAt)!.DurationMs);

        return new SetlistDto(setlist.Id, setlist.Name, setlist.Date, setlist.Venue, setlist.CreatedAt, setlist.UpdatedAt,
        [
            .. items.Select(i =>
            {
                var version = played[i.SongId];
                return new SetlistItemDto(i.Position, i.SongId, i.SongTitle, i.VersionId, version?.Id,
                    [.. versions[i.SongId].Select(v => new SetlistVersionOptionDto(v.Id, v.Number, v.Title))],
                    version?.Key?.Value, version?.Bpm, i.Notes, i.DurationMs,
                    version is not null ? durations.GetValueOrDefault(version.Id) : null);
            }),
        ]);
    }

    public async Task<IReadOnlyList<string>> ListNamesWithSongAsync(Guid songId, CancellationToken ct)
        => await BandSetlists.AsNoTracking()
            .Where(s => s.Items.Any(i => i.SongId == songId))
            .OrderBy(s => s.Name)
            .Select(s => s.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> ListNamesWithVersionAsync(Guid versionId, CancellationToken ct)
        => await BandSetlists.AsNoTracking()
            .Where(s => s.Items.Any(i => i.VersionId == versionId))
            .OrderBy(s => s.Name)
            .Select(s => s.Name)
            .ToListAsync(ct);
}