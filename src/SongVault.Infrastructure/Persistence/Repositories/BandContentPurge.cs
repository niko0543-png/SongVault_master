using Microsoft.EntityFrameworkCore;

using SongVault.Domain.Releases;

namespace SongVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// Efface des groupes et tout leur contenu (ADR 0048). Partagé par la purge d'un compte (ses groupes solo, ADR 0011)
/// et la suppression d'un groupe par son Owner. À appeler dans une transaction ouverte par l'appelant.
/// </summary>
internal static class BandContentPurge
{
    /// <summary>
    /// Efface les lignes et renvoie les clés des fichiers à effacer du stockage ensuite : fichiers des morceaux
    /// (corbeille comprise), MP3 d'aperçu, pochettes des sorties, archives des sorties.
    /// Le reste part en cascade avec le groupe : adhésions, invitations, liens de partage, abonnement,
    /// fil d'activité, notifications, journal d'audit (son déclencheur laisse passer les lignes d'un groupe effacé).
    /// </summary>
    public static async Task<List<string>> DeleteAsync(SongVaultDbContext db, IReadOnlyCollection<Guid> bandIds, CancellationToken ct)
    {
        if (bandIds.Count == 0) return [];

        // #20 : corbeille comprise, sinon ses fichiers resteraient dans le stockage et ses morceaux bloqueraient le groupe
        var songs = db.Songs.IgnoreQueryFilters([SongVaultDbContext.BandFilter, SongVaultDbContext.SoftDeleteFilter])
            .Where(s => bandIds.Contains(s.BandId));
        var files = songs.SelectMany(s => s.Versions).SelectMany(v => v.Files);
        var keys = await files.Select(f => f.StorageKey).ToListAsync(ct);
        // #15 : leurs MP3 d'aperçu (les lignes partent en cascade avec les fichiers)
        keys.AddRange(await db.AudioPreviews
            .Where(p => p.PreviewStorageKey != null && files.Any(f => f.Id == p.SongFileId))
            .Select(p => p.PreviewStorageKey!)
            .ToListAsync(ct));
        // Sorties de ces groupes (#10) : leurs pochettes, puis les sorties avant les morceaux que citent leurs pistes
        var releases = db.Releases.IgnoreQueryFilters([SongVaultDbContext.BandFilter])
            .Where(r => bandIds.Contains(r.BandId));
        keys.AddRange(await releases
            .Where(r => r.CoverStorageKey != null)
            .Select(r => r.CoverStorageKey!)
            .ToListAsync(ct));

        // #24 : archives des sorties de ces groupes
        var releaseExports = db.ReleaseExports.Where(e => releases.Any(r => r.Id == e.ReleaseId));
        keys.AddRange((await releaseExports.Select(e => e.Id).ToListAsync(ct)).Select(ReleaseExport.StorageKeyFor));

        await releaseExports.ExecuteDeleteAsync(ct);
        await releases.ExecuteDeleteAsync(ct);                 // pistes en cascade
        // Setlists (#21) : avant les morceaux que citent leurs lignes (lignes en cascade)
        await db.Setlists.IgnoreQueryFilters([SongVaultDbContext.BandFilter])
            .Where(s => bandIds.Contains(s.BandId))
            .ExecuteDeleteAsync(ct);
        await songs.ExecuteDeleteAsync(ct);
        await db.Bands.Where(b => bandIds.Contains(b.Id)).ExecuteDeleteAsync(ct);
        return keys;
    }
}
