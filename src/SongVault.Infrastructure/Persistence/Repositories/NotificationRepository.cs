using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Notifications;
using SongVault.Domain.Activity;
using SongVault.Domain.Notifications;

namespace SongVault.Infrastructure.Persistence.Repositories;

/// <summary>Hors requête HTTP : ne passe ni par le groupe actif ni par le filtre de groupe (ADR 0018).</summary>
internal sealed class NotificationRepository(SongVaultDbContext db) : INotificationRepository
{
    public async Task<IReadOnlyList<NotificationPreference>> ListPreferencesAsync(string userId, CancellationToken ct)
        => await db.NotificationPreferences.Where(p => p.UserId == userId).ToListAsync(ct);

    public void AddPreference(NotificationPreference preference) => db.NotificationPreferences.Add(preference);

    public async Task<IReadOnlyDictionary<(string UserId, NotificationKind Kind), NotificationMode>> GetModesAsync(
        IReadOnlyCollection<string> userIds, CancellationToken ct)
        => await db.NotificationPreferences.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .ToDictionaryAsync(p => (p.UserId, p.Kind), p => p.Mode, ct);

    public async Task<IReadOnlyList<long>> ClaimEventsAsync(int take, DateTimeOffset now, CancellationToken ct)
        // READPAST : saute les lignes verrouillées, donc les événements pas encore validés et ceux qu'une autre
        // instance est en train de prendre. Ils restent à NotifiedAt NULL et seront pris au tour suivant.
        => await db.Database.SqlQuery<long>($"""
            WITH next AS (
                SELECT TOP ({take}) Id, NotifiedAt FROM ActivityEvents WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE NotifiedAt IS NULL
                ORDER BY Id)
            UPDATE next SET NotifiedAt = {now}
            OUTPUT inserted.Id AS Value
            """).ToListAsync(ct);

    public async Task<IReadOnlyList<NotificationEvent>> GetEventsAsync(
        IReadOnlyCollection<long> eventIds, CancellationToken ct)
     => await Events(db.ActivityEvents.Where(e => eventIds.Contains(e.Id))).ToListAsync(ct);

    public async Task<IReadOnlyList<NotificationRecipient>> ListMembersAsync(Guid bandId, CancellationToken ct)
        => await (from m in db.BandMemberships.AsNoTracking()
                  join u in db.Users on m.UserId equals u.Id
                  where m.BandId == bandId && u.Email != null
                  select new NotificationRecipient(u.Id, u.Email!))
            .ToListAsync(ct);

    public void AddDigestItem(NotificationDigestItem item) => db.NotificationDigestItems.Add(item);

    public Task<NotificationRecipient?> GetMemberAsync(Guid bandId, string userId, CancellationToken ct)
        => (from m in db.BandMemberships.AsNoTracking()
            join u in db.Users on m.UserId equals u.Id
            where m.BandId == bandId && m.UserId == userId && u.Email != null
            select new NotificationRecipient(u.Id, u.Email!))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<string>> ListDigestUsersAsync(CancellationToken ct)
        => await db.NotificationDigestItems.Select(i => i.UserId).Distinct().ToListAsync(ct);

    public async Task<IReadOnlyList<DigestEntry>> ListDigestAsync(string userId, CancellationToken ct)
    {
        var items = await db.NotificationDigestItems.AsNoTracking()
            .Where(i => i.UserId == userId)
            .OrderBy(i => i.Id)
            .Select(i => new { i.Id, i.Kind, i.EventId })
            .ToListAsync(ct);
        var eventIds = items.Select(i => i.EventId).Distinct().ToList();
        var events = (await GetEventsAsync(eventIds, ct)).ToDictionary(e => e.Id);
        var bandIds = await db.BandMemberships.Where(m => m.UserId == userId).Select(m => m.BandId).ToListAsync(ct);

        return [.. items
            .Where(i => events.ContainsKey(i.EventId))
            .Select(i => new DigestEntry(i.Id, i.Kind, events[i.EventId], bandIds.Contains(events[i.EventId].BandId)))];
    }

    public Task<string?> GetEmailAsync(string userId, CancellationToken ct)
        => db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync(ct);

    public Task DeleteDigestItemsAsync(IReadOnlyCollection<long> itemIds, CancellationToken ct)
        => db.NotificationDigestItems.Where(i => itemIds.Contains(i.Id)).ExecuteDeleteAsync(ct);

    /// <summary>L'événement, son groupe, l'e-mail de son auteur et le texte du commentaire, en une requête.</summary>
    private IQueryable<NotificationEvent> Events(IQueryable<ActivityEvent> events)
        => from e in events.AsNoTracking()
           join b in db.Bands on e.BandId equals b.Id
           orderby e.Id
           select new NotificationEvent(
               e.Id, e.BandId, b.Name, e.Type, e.ActorId,
               db.Users.Where(u => u.Id == e.ActorId).Select(u => u.Email).FirstOrDefault(),
               e.SongId, e.VersionId, e.Details.SongTitle, e.Details.VersionNumber, e.Details.VersionTitle,
               e.Details.FileName,
               e.Type == ActivityType.CommentAdded || e.Type == ActivityType.CommentReplied
                   ? db.Comments.Where(c => c.Id == e.EntityId).Select(c => c.Body).FirstOrDefault()
                   : null,
               e.CreatedAt);
}