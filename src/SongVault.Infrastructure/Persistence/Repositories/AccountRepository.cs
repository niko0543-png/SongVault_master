using Microsoft.EntityFrameworkCore;
using SongVault.Application.Abstractions;
using SongVault.Application.Accounts;
using SongVault.Domain.Accounts;
using SongVault.Domain.Invitations;
using Microsoft.AspNetCore.Identity;
using SongVault.Domain.Bands;

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
        var songs = await db.Songs.AsNoTracking()
            .IgnoreQueryFilters([SongVaultDbContext.BandFilter])
            .Where(s => bandIds.Contains(s.BandId))
            .Include(s => s.Versions).ThenInclude(v => v.Files)
            .AsSplitQuery()
            .ToListAsync(ct);

        var sent = await db.Invitations.AsNoTracking()
            .Where(i => i.InvitedById == userId)
            .Join(db.Bands, i => i.BandId, b => b.Id, (i, b) => new { Invitation = i, BandName = b.Name })
            .ToListAsync(ct);
        var normalizedEmail = email.ToLowerInvariant();            // Invitations.Email est en minuscules
        var received = await db.Invitations.AsNoTracking()
            .Where(i => i.Email == normalizedEmail)
            .Join(db.Bands, i => i.BandId, b => b.Id, (i, b) => new { Invitation = i, BandName = b.Name })
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
                        v.Files.OrderBy(f => f.UploadedAt).Select(f => new AccountFileData(
                            f.Id, f.OriginalFileName, f.ContentType, f.SizeBytes, f.UploadedAt, f.StorageKey)).ToList()))
                        .ToList()))
                    .ToList()))
                .ToList(),
            sent.OrderBy(x => x.Invitation.CreatedAt).Select(x => ToData(x.Invitation, x.BandName)).ToList(),
            received.OrderBy(x => x.Invitation.CreatedAt).Select(x => ToData(x.Invitation, x.BandName)).ToList());
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

        var songs = db.Songs.IgnoreQueryFilters([SongVaultDbContext.BandFilter])
            .Where(s => soloBandIds.Contains(s.BandId));
        var keys = await songs.SelectMany(s => s.Versions).SelectMany(v => v.Files)
            .Select(f => f.StorageKey)
            .ToListAsync(ct);
        keys.AddRange(await db.DataExports
            .Where(e => e.UserId == userId && e.StorageKey != null)
            .Select(e => e.StorageKey!)
            .ToListAsync(ct));

        var email = (user.Email ?? "").ToLowerInvariant();     // Invitations.Email est en minuscules
        await songs.ExecuteDeleteAsync(ct);
        await db.Bands.Where(b => soloBandIds.Contains(b.Id)).ExecuteDeleteAsync(ct);
        await db.Invitations.Where(i => i.Email == email).ExecuteDeleteAsync(ct);
        await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
        return keys;
    }
}