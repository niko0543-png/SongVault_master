using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Accounts;
using SongVault.Domain.Accounts;
using SongVault.Domain.Invitations;

namespace SongVault.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository(SongVaultDbContext db) : IAccountRepository
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
}