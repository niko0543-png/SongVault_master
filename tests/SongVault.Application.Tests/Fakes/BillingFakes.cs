using SongVault.Application.Abstractions;
using SongVault.Domain.Billing;

namespace SongVault.Application.Tests.Fakes;

/// <summary>Formules, abonnements et stockage en mémoire. Les formules ont de petits quotas, en octets.</summary>
internal sealed class FakeBillingRepository : IBillingRepository
{
    public List<Plan> Plans { get; } =
    [
        Plan.Create(Plan.FreeId, "Gratuit", 1000, maxBandsPerOwner: 1, monthlyPriceCents: 0, sortOrder: 1),
        Plan.Create("band", "Groupe", 10_000, maxBandsPerOwner: null, monthlyPriceCents: 600, sortOrder: 2),
        Plan.Create("studio", "Studio", 100_000, maxBandsPerOwner: null, monthlyPriceCents: 1500, sortOrder: 3),
    ];
    public List<Subscription> Subscriptions { get; } = [];
    /// <summary>Stockage utilisé par groupe ; un groupe absent n'existe pas.</summary>
    public Dictionary<Guid, long> StorageUsed { get; } = [];
    public Dictionary<Guid, BandOwners> Owners { get; } = [];
    public HashSet<string> Processed { get; } = [];
    public List<string> ToCancel { get; } = [];
    public int OwnedFreeBands { get; set; }
    public List<Guid> Refreshed { get; } = [];

    public Task<IReadOnlyList<Plan>> ListPlansAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Plan>>([.. Plans.OrderBy(p => p.SortOrder)]);

    public Task<Plan?> GetPlanAsync(string planId, CancellationToken ct) => Task.FromResult(Plans.SingleOrDefault(p => p.Id == planId));

    public Task<Subscription?> GetSubscriptionAsync(Guid bandId, CancellationToken ct)
        => Task.FromResult(Subscriptions.SingleOrDefault(s => s.BandId == bandId));

    public void AddSubscription(Subscription subscription) => Subscriptions.Add(subscription);

    public Task<IReadOnlyList<Subscription>> ListSubscriptionsAsync(IReadOnlyCollection<Guid> bandIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Subscription>>([.. Subscriptions.Where(s => bandIds.Contains(s.BandId))]);

    public Task<IReadOnlyList<string>> ListStripeSubscriptionsToCancelAsync(string userId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<string>>(ToCancel);

    public Task<long?> GetStorageUsedAsync(Guid bandId, CancellationToken ct)
        => Task.FromResult(StorageUsed.TryGetValue(bandId, out var used) ? used : (long?)null);

    public Task RefreshStorageUsedAsync(Guid bandId, CancellationToken ct)
    {
        Refreshed.Add(bandId);
        return Task.CompletedTask;
    }

    public Task<int> RefreshAllStorageUsedAsync(CancellationToken ct) => Task.FromResult(StorageUsed.Count);

    public Task<int> CountOwnedBandsOnFreePlanAsync(string userId, CancellationToken ct) => Task.FromResult(OwnedFreeBands);

    public Task<bool> BandExistsAsync(Guid bandId, CancellationToken ct) => Task.FromResult(StorageUsed.ContainsKey(bandId));

    public Task<BandOwners?> GetBandOwnersAsync(Guid bandId, CancellationToken ct)
        => Task.FromResult(Owners.TryGetValue(bandId, out var owners) ? owners : null);

    public Task<bool> IsEventProcessedAsync(string eventId, CancellationToken ct) => Task.FromResult(Processed.Contains(eventId));

    public void AddProcessedEvent(ProcessedStripeEvent processed) => Processed.Add(processed.EventId);
}