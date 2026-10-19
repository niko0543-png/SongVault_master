using Microsoft.EntityFrameworkCore;
using SongVault.Application.Abstractions;
using SongVault.Application.Accounts;
using SongVault.Domain.Accounts;
using SongVault.Domain.Invitations;
using Microsoft.AspNetCore.Identity;
using SongVault.Domain.Bands;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository(SongVaultDbContext db, UserManager<IdentityUser> users) : IAccountRepository
{
    public void AddExport(DataExport export) => db.DataExports.Add(export);

    public void RemoveExport(DataExport export) => db.DataExports.Remove(export);

    public Task<DataExport?> GetExportAsync(Guid exportId, CancellationToken ct)
        => db.DataExports.FirstOrDefaultAsync(e => e.Id == exportId, ct);

    public Task<DataExport?> GetLatestExportAsync(string userId, CancellationToken ct)
        => db.DataExports
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.RequestedAt).ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<AccountData?> ReadAccountDataAsync(string userId, DateTimeOffset now, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return null;
        var email = user.Email ?? "";

        var memberships = await db.BandMemberships.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(db.Bands, m => m.BandId, b => b.Id, (m, b) => new { b.Id, b.Name, m.Role, m.JoinedAt })
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
        var bandIds = memberships.Select(m => m.Id).ToList();

        // Pas de groupe actif hors requête HTTP : on ôte le filtre et on choisit les groupes nous-mêmes
        // #20 : la corbeille aussi, ce sont encore ses données
        var songs = await db.Songs.AsNoTracking()
            .IgnoreQueryFilters([SongVaultDbContext.BandFilter, SongVaultDbContext.SoftDeleteFilter])
            .Where(s => bandIds.Contains(s.BandId))
            .Include(s => s.Versions).ThenInclude(v => v.Files)
            .AsSplitQuery()
            .ToListAsync(ct);

        // #17 : seuls les fichiers déclarés sains par l'antivirus (ou non analysés, antivirus désactivé) partent dans l'archive
        var cleanFileIds = (await (
                from scan in db.FileScans.AsNoTracking()
                join f in db.Set<SongFile>() on scan.SongFileId equals f.Id
                join v in db.Set<SongVersion>() on f.SongVersionId equals v.Id
                join s in db.Songs.IgnoreQueryFilters([SongVaultDbContext.BandFilter, SongVaultDbContext.SoftDeleteFilter]) on v.SongId equals s.Id
                where (scan.Status == FileScanStatus.Clean || scan.Status == FileScanStatus.NotScanned) && bandIds.Contains(s.BandId)
                select f.Id)
            .ToListAsync(ct)).ToHashSet();

        var sent = await db.Invitations.AsNoTracking()
            .Where(i => i.InvitedById == userId)
            .Join(db.Bands, i => i.BandId, b => b.Id, (i, b) => new { Invitation = i, BandName = b.Name })
            .ToListAsync(ct);
        var normalizedEmail = email.ToLowerInvariant();            // Invitations.Email est en minuscules
        var received = await db.Invitations.AsNoTracking()
            .Where(i => i.Email == normalizedEmail)
            .Join(db.Bands, i => i.BandId, b => b.Id, (i, b) => new { Invitation = i, BandName = b.Name })
            .ToListAsync(ct);

        // Commentaires écrits par le compte, dans tous les groupes, même ceux qu'il a quittés (ADR 0014)
        var comments = await (
                from c in db.Comments.AsNoTracking()
                join f in db.Set<SongFile>() on c.SongFileId equals f.Id
                join v in db.Set<SongVersion>() on f.SongVersionId equals v.Id
                join s in db.Songs.IgnoreQueryFilters([SongVaultDbContext.BandFilter, SongVaultDbContext.SoftDeleteFilter]) on v.SongId equals s.Id
                join b in db.Bands on s.BandId equals b.Id
                where c.AuthorId == userId
                orderby c.CreatedAt
                select new AccountCommentData(c.Id, b.Name, s.Title, v.Number, f.OriginalFileName,
                    c.ParentId, c.PositionMs, c.Body, c.CreatedAt, c.EditedAt))
            .ToListAsync(ct);


        AccountInvitationData ToData(Invitation i, string bandName)
            => new(i.BandId, bandName, i.Email, i.Role, i.CreatedAt, i.StatusAt(now));

        return new AccountData(
            user.Id, email, user.EmailConfirmed,
            memberships.Select(m => new AccountBandData(m.Id, m.Name, m.Role, m.JoinedAt,
                songs.Where(s => s.BandId == m.Id).OrderBy(s => s.Title).Select(s => new AccountSongData(
                    s.Id, s.Title, s.Artist, s.Description, s.CreatedAt, s.UpdatedAt,
                    s.Versions.OrderBy(v => v.Number).Select(v => new AccountVersionData(
                        v.Id, v.Number, v.Title, v.Status, v.Bpm, v.Key?.Value, v.Notes, v.Lyrics, v.CreatedAt, v.UpdatedAt,
                         v.Files.Where(f => cleanFileIds.Contains(f.Id)).OrderBy(f => f.UploadedAt).Select(f => new AccountFileData(
                            f.Id, f.OriginalFileName, f.ContentType, f.SizeBytes, f.UploadedAt, f.StorageKey)).ToList()))
                        .ToList()))
                    .ToList()))
                .ToList(),
            sent.OrderBy(x => x.Invitation.CreatedAt).Select(x => ToData(x.Invitation, x.BandName)).ToList(),
                        received.OrderBy(x => x.Invitation.CreatedAt).Select(x => ToData(x.Invitation, x.BandName)).ToList(),
            comments);
    }


    public async Task<bool> CheckPasswordAsync(string userId, string password, CancellationToken ct)
        => await users.FindByIdAsync(userId) is { } user && await users.CheckPasswordAsync(user, password);

    public async Task<IReadOnlyList<Band>> ListBandsAsync(string userId, CancellationToken ct)
        => await db.Bands.Include(b => b.Memberships)
            .Where(b => b.Memberships.Any(m => m.UserId == userId))
            .ToListAsync(ct);

    public async Task DeactivateAsync(string userId, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;              // la connexion répond « Lockedout »
        user.SecurityStamp = Guid.NewGuid().ToString("N");      // autres sessions coupées à la prochaine vérification (1 min)
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
    }

    public async Task<IReadOnlyList<string>> PurgeAsync(string userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || user.LockoutEnd != DateTimeOffset.MaxValue) return [];   // déjà purgé, ou réactivé

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Groupes dont il est le seul membre (les groupes partagés, il les a quittés à la demande)
        var soloBandIds = await db.BandMemberships
            .Where(m => m.UserId == userId && db.BandMemberships.Count(o => o.BandId == m.BandId) == 1)
            .Select(m => m.BandId)
            .ToListAsync(ct);

        // #20 : corbeille comprise, sinon ses fichiers resteraient dans le stockage et ses morceaux bloqueraient le groupe
        var songs = db.Songs.IgnoreQueryFilters([SongVaultDbContext.BandFilter, SongVaultDbContext.SoftDeleteFilter])
            .Where(s => soloBandIds.Contains(s.BandId));
        var files = songs.SelectMany(s => s.Versions).SelectMany(v => v.Files);
        var keys = await files.Select(f => f.StorageKey).ToListAsync(ct);
        // #15 : leurs MP3 d'aperçu (les lignes partent en cascade avec les fichiers)
        keys.AddRange(await db.AudioPreviews
            .Where(p => p.PreviewStorageKey != null && files.Any(f => f.Id == p.SongFileId))
            .Select(p => p.PreviewStorageKey!)
            .ToListAsync(ct));
        // Sorties de ces groupes (#10) : leurs pochettes, puis les sorties avant les morceaux que citent leurs pistes
        var releases = db.Releases.IgnoreQueryFilters([SongVaultDbContext.BandFilter])
            .Where(r => soloBandIds.Contains(r.BandId));
        keys.AddRange(await releases
            .Where(r => r.CoverStorageKey != null)
            .Select(r => r.CoverStorageKey!)
            .ToListAsync(ct));
        keys.AddRange(await db.DataExports
            .Where(e => e.UserId == userId && e.StorageKey != null)
            .Select(e => e.StorageKey!)
            .ToListAsync(ct));

        var email = (user.Email ?? "").ToLowerInvariant();     // Invitations.Email est en minuscules
        await releases.ExecuteDeleteAsync(ct);                 // pistes en cascade
        // Setlists (#21) : avant les morceaux que citent leurs lignes (lignes en cascade)
        await db.Setlists.IgnoreQueryFilters([SongVaultDbContext.BandFilter])
            .Where(s => soloBandIds.Contains(s.BandId))
            .ExecuteDeleteAsync(ct);
        await songs.ExecuteDeleteAsync(ct);
        await db.Bands.Where(b => soloBandIds.Contains(b.Id)).ExecuteDeleteAsync(ct);
        await db.Invitations.Where(i => i.Email == email).ExecuteDeleteAsync(ct);
        // Commentaires dans les groupes partagés : gardés pour le fil de discussion, sans auteur (ADR 0014).
        // Ceux des groupes effacés juste au-dessus sont partis avec leurs fichiers.
        await db.Comments.Where(c => c.AuthorId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.AuthorId, (string?)null), ct);
        // Fil d'activité des groupes partagés : gardé, sans auteur (« Compte supprimé »), comme les commentaires.
        // Les événements des groupes effacés plus haut sont partis avec eux (cascade sur BandId).
        await db.ActivityEvents.Where(e => e.ActorId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.ActorId, (string?)null), ct);
        await db.SongViews.Where(v => v.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Comments.Where(c => c.ResolvedById == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ResolvedById, (string?)null), ct);
        await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
        return keys;
    }
}