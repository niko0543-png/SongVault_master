using SongVault.Application.Abstractions;
using SongVault.Application.Accounts;
using SongVault.Application.Activity;
using SongVault.Application.Audit;
using SongVault.Domain.Accounts;
using SongVault.Domain.Audit;

namespace SongVault.Application.Tests.Fakes;

/// <summary>Journal d'audit en mémoire (#19) : Entries reçoit ce que les handlers écrivent, Items ce que ListAsync rend.</summary>
internal sealed class FakeAuditRepository : IAuditRepository
{
    public List<AuditEntry> Entries { get; } = [];
    public List<AuditItemDto> Items { get; } = [];

    public void Add(AuditEntry entry) => Entries.Add(entry);

    public Task<IReadOnlyList<AuditItemDto>> ListAsync(ActivityCursor? after, int take, CancellationToken ct)
    {
        IReadOnlyList<AuditItemDto> page = [.. Items
            .Where(i => after is not { } c || i.OccurredAt < c.CreatedAt || (i.OccurredAt == c.CreatedAt && i.Id < c.Id))
            .OrderByDescending(i => i.OccurredAt).ThenByDescending(i => i.Id)
            .Take(take)];
        return Task.FromResult(page);
    }
}

/// <summary>Historique de connexion en mémoire (#19).</summary>
internal sealed class FakeAccountEventRepository : IAccountEventRepository
{
    public List<AccountEvent> Events { get; } = [];

    public void Add(AccountEvent accountEvent) => Events.Add(accountEvent);

    public Task<IReadOnlyList<AccountEventDto>> ListAsync(string userId, int take, CancellationToken ct)
    {
        IReadOnlyList<AccountEventDto> list = [.. Events
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.OccurredAt)
            .Take(take)
            .Select(e => new AccountEventDto(e.Id, e.Type, e.OccurredAt, e.Network, e.Device))];
        return Task.FromResult(list);
    }
}