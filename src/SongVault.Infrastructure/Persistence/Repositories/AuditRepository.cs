using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Accounts;
using SongVault.Application.Activity;
using SongVault.Application.Audit;
using SongVault.Domain.Accounts;
using SongVault.Domain.Audit;

namespace SongVault.Infrastructure.Persistence.Repositories;

/// <summary>Toutes les lectures partent de BandEntries : seulement le groupe actif, vérifié par [BandScoped].</summary>
internal sealed class AuditRepository(SongVaultDbContext db, IBandContext bandContext) : IAuditRepository
{
    private IQueryable<AuditEntry> BandEntries => db.AuditEntries.Where(e => e.BandId == bandContext.RequiredBandId);

    public void Add(AuditEntry entry) => db.AuditEntries.Add(entry);

    public async Task<IReadOnlyList<AuditItemDto>> ListAsync(ActivityCursor? after, int take, CancellationToken ct)
    {
        var query = BandEntries.AsNoTracking();
        if (after is { } c)
            query = query.Where(e => e.OccurredAt < c.CreatedAt || (e.OccurredAt == c.CreatedAt && e.Id < c.Id));

        return await query
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .Take(take)
            .Select(e => new AuditItemDto(
                e.Id, e.Action, e.ActorId,
                db.Users.Where(u => u.Id == e.ActorId).Select(u => u.Email).FirstOrDefault(),
                e.Subject, e.TargetUserId,
                db.Users.Where(u => u.Id == e.TargetUserId).Select(u => u.Email).FirstOrDefault(),
                e.Details, e.OccurredAt))
            .ToListAsync(ct);
    }
}

internal sealed class AccountEventRepository(SongVaultDbContext db) : IAccountEventRepository
{
    public void Add(AccountEvent accountEvent) => db.AccountEvents.Add(accountEvent);

    public async Task<IReadOnlyList<AccountEventDto>> ListAsync(string userId, int take, CancellationToken ct)
        => await db.AccountEvents.AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .Take(take)
            .Select(e => new AccountEventDto(e.Id, e.Type, e.OccurredAt, e.Network, e.Device))
            .ToListAsync(ct);
}