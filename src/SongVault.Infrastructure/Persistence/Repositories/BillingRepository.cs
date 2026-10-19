using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Domain.Bands;
using SongVault.Domain.Billing;

namespace SongVault.Infrastructure.Persistence.Repositories;

internal sealed class BillingRepository(SongVaultDbContext db) : IBillingRepository
{
    private static readonly SubscriptionStatus[] LiveStatuses =
        [SubscriptionStatus.Active, SubscriptionStatus.Trialing, SubscriptionStatus.PastDue];

    public async Task<IReadOnlyList<Plan>> ListPlansAsync(CancellationToken ct)
        => await db.Plans.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync(ct);

    public Task<Plan?> GetPlanAsync(string planId, CancellationToken ct)
        => db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId, ct);

    public Task<Subscription?> GetSubscriptionAsync(Guid bandId, CancellationToken ct)
        => db.Subscriptions.FirstOrDefaultAsync(s => s.BandId == bandId, ct);

    public void AddSubscription(Subscription subscription) => db.Subscriptions.Add(subscription);

    public async Task<IReadOnlyList<Subscription>> ListSubscriptionsAsync(IReadOnlyCollection<Guid> bandIds, CancellationToken ct)
        => bandIds.Count == 0 ? [] : await db.Subscriptions.Where(s => bandIds.Contains(s.BandId)).ToListAsync(ct);

    public async Task<IReadOnlyList<string>> ListStripeSubscriptionsToCancelAsync(string userId, CancellationToken ct)
        => await (
                from s in db.Subscriptions
                join m in db.BandMemberships on s.BandId equals m.BandId
                join u in db.Users on m.UserId equals u.Id
                where m.UserId == userId
                      && u.LockoutEnd == DateTimeOffset.MaxValue                 // toujours désactivé (ADR 0011)
                      && db.BandMemberships.Count(o => o.BandId == s.BandId) == 1
                      && s.StripeSubscriptionId != null
                      && LiveStatuses.Contains(s.Status)
                select s.StripeSubscriptionId!)
            .ToListAsync(ct);

    public Task<long?> GetStorageUsedAsync(Guid bandId, CancellationToken ct)
        => db.Bands.AsNoTracking().Where(b => b.Id == bandId).Select(b => (long?)b.StorageUsedBytes).FirstOrDefaultAsync(ct);

    public Task RefreshStorageUsedAsync(Guid bandId, CancellationToken ct)
        => RefreshAsync(db.Bands.Where(b => b.Id == bandId), ct);

    public Task<int> RefreshAllStorageUsedAsync(CancellationToken ct) => RefreshAsync(db.Bands, ct);

    // Une instruction : UPDATE Bands SET StorageUsedBytes = (SELECT SUM(...) des fichiers de ses morceaux, corbeille comprise).
    // Elle lit les fichiers enregistrés, donc deux envois simultanés ne se marchent pas dessus.
    private Task<int> RefreshAsync(IQueryable<Band> bands, CancellationToken ct)
        => bands.ExecuteUpdateAsync(s => s.SetProperty(b => b.StorageUsedBytes, b =>
            // Hors requête HTTP aussi (tâche de nuit) ; la corbeille compte jusqu'à la purge (#20)
            db.Songs.IgnoreQueryFilters(new[] { SongVaultDbContext.BandFilter, SongVaultDbContext.SoftDeleteFilter })
                .Where(song => song.BandId == b.Id)
                .SelectMany(song => song.Versions)
                .SelectMany(v => v.Files)
                .Sum(f => (long?)f.SizeBytes) ?? 0L), ct);

    public Task<int> CountOwnedBandsOnFreePlanAsync(string userId, CancellationToken ct)
        => db.BandMemberships
            .Where(m => m.UserId == userId && m.Role == BandRole.Owner)
            .CountAsync(m => !db.Subscriptions.Any(s => s.BandId == m.BandId && LiveStatuses.Contains(s.Status)), ct);

    public Task<bool> BandExistsAsync(Guid bandId, CancellationToken ct) => db.Bands.AnyAsync(b => b.Id == bandId, ct);

    public async Task<BandOwners?> GetBandOwnersAsync(Guid bandId, CancellationToken ct)
    {
        var name = await db.Bands.AsNoTracking().Where(b => b.Id == bandId).Select(b => b.Name).FirstOrDefaultAsync(ct);
        if (name is null) return null;

        var emails = await db.BandMemberships.AsNoTracking()
            .Where(m => m.BandId == bandId && m.Role == BandRole.Owner)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => u.Email)
            .Where(e => e != null)
            .Select(e => e!)
            .ToListAsync(ct);
        return new BandOwners(name, emails);
    }

    public Task<bool> IsEventProcessedAsync(string eventId, CancellationToken ct)
        => db.ProcessedStripeEvents.AnyAsync(e => e.EventId == eventId, ct);

    public void AddProcessedEvent(ProcessedStripeEvent processed) => db.ProcessedStripeEvents.Add(processed);
}