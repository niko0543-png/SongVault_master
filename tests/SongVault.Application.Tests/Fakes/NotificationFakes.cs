using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Notifications;
using SongVault.Domain.Notifications;

namespace SongVault.Application.Tests.Fakes;

/// <summary>Notifications en mémoire : on remplit Events, Members et Preferences, on lit Notified et Digest.</summary>
internal sealed class FakeNotificationRepository : INotificationRepository
{
    public List<NotificationEvent> Events { get; } = [];
    public HashSet<long> Notified { get; } = [];
    public Dictionary<Guid, List<NotificationRecipient>> Members { get; } = [];
    public List<NotificationPreference> Preferences { get; } = [];
    /// <summary>Éléments de résumé, avec l'identifiant qu'aurait donné la base.</summary>
    public List<(long Id, NotificationDigestItem Item)> Digest { get; } = [];
    public IEnumerable<NotificationDigestItem> DigestItems => Digest.Select(x => x.Item);
    /// <summary>Comptes qui n'existent plus (purgés).</summary>
    public HashSet<string> DeletedUsers { get; } = [];

    public Task<IReadOnlyList<NotificationPreference>> ListPreferencesAsync(string userId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<NotificationPreference>>([.. Preferences.Where(p => p.UserId == userId)]);

    public void AddPreference(NotificationPreference preference) => Preferences.Add(preference);

    public Task<IReadOnlyDictionary<(string UserId, NotificationKind Kind), NotificationMode>> GetModesAsync(
        IReadOnlyCollection<string> userIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<(string, NotificationKind), NotificationMode>>(Preferences
            .Where(p => userIds.Contains(p.UserId))
            .ToDictionary(p => (p.UserId, p.Kind), p => p.Mode));

    public Task<IReadOnlyList<long>> ClaimEventsAsync(int take, DateTimeOffset now, CancellationToken ct)
    {
        IReadOnlyList<long> ids = [.. Events.Select(e => e.Id).Where(id => !Notified.Contains(id)).Order().Take(take)];
        Notified.UnionWith(ids);
        return Task.FromResult(ids);
    }

    public Task<IReadOnlyList<NotificationEvent>> GetEventsAsync(IReadOnlyCollection<long> eventIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<NotificationEvent>>([.. Events.Where(e => eventIds.Contains(e.Id)).OrderBy(e => e.Id)]);

    public Task<IReadOnlyList<NotificationRecipient>> ListMembersAsync(Guid bandId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<NotificationRecipient>>(Members.GetValueOrDefault(bandId) ?? []);

    private long _nextDigestId;
    public void AddDigestItem(NotificationDigestItem item) => Digest.Add((++_nextDigestId, item));

    public Task<NotificationRecipient?> GetMemberAsync(Guid bandId, string userId, CancellationToken ct)
        => Task.FromResult(Members.GetValueOrDefault(bandId)?.FirstOrDefault(m => m.UserId == userId));

    public Task<IReadOnlyList<string>> ListDigestUsersAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<string>>([.. DigestItems.Select(i => i.UserId).Distinct()]);

    public Task<IReadOnlyList<DigestEntry>> ListDigestAsync(string userId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<DigestEntry>>([.. Digest
            .Where(x => x.Item.UserId == userId)
            .Select(x => (x.Id, x.Item, Event: Events.Single(e => e.Id == x.Item.EventId)))
            .Select(x => new DigestEntry(x.Id, x.Item.Kind, x.Event,
                Members.GetValueOrDefault(x.Event.BandId)?.Any(m => m.UserId == userId) ?? false))]);

    public Task<string?> GetEmailAsync(string userId, CancellationToken ct)
        => Task.FromResult(DeletedUsers.Contains(userId) ? null : $"{userId}@test.local");

    public Task DeleteDigestItemsAsync(IReadOnlyCollection<long> itemIds, CancellationToken ct)
    {
        Digest.RemoveAll(x => itemIds.Contains(x.Id));
        return Task.CompletedTask;
    }
}

/// <summary>Tâches de fond en mémoire.</summary>
internal sealed class FakeBackgroundJobs : IBackgroundJobs
{
    public List<(string Type, string Payload, DateTimeOffset RunAfter)> Enqueued { get; } = [];

    public void Enqueue(string type, string payload, DateTimeOffset runAfter) => Enqueued.Add((type, payload, runAfter));

    public Task<bool> HasPendingAsync(string type, CancellationToken ct) => Task.FromResult(Enqueued.Any(j => j.Type == type));
}

/// <summary>Garde les e-mails ; FailFor : adresses que le fournisseur refuse.</summary>
internal sealed class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];
    public HashSet<string> FailFor { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (FailFor.Contains(message.To))
            throw new EmailDeliveryException("Refusé par le test.");
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

/// <summary>Jetons lisibles : « userId|type ».</summary>
internal sealed class FakeUnsubscribeTokens : IUnsubscribeTokens
{
    public string Create(string userId, NotificationKind? kind) => $"{userId}|{kind?.ToString() ?? "*"}";

    public bool TryRead(string token, out string userId, out NotificationKind? kind)
    {
        userId = "";
        kind = null;
        if (token.Split('|') is not [var user, var k]) return false;
        userId = user;
        if (k == "*") return true;
        if (!Enum.TryParse<NotificationKind>(k, out var parsed)) return false;
        kind = parsed;
        return true;
    }
}