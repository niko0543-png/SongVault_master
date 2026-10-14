using System.Text.Json;

using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Domain.Notifications;

namespace SongVault.Application.Notifications;

/// <summary>
/// Prépare les notifications des nouveaux événements du fil (ADR 0018) : une tâche d'envoi par e-mail immédiat,
/// un élément de résumé sinon. Appelé en boucle par NotificationWorker, jamais pendant une requête HTTP.
/// </summary>
public sealed class NotificationDispatcher(
    INotificationRepository notifications, IBackgroundJobs jobs, IUnitOfWork unitOfWork,
    IOptions<NotificationOptions> options, TimeProvider clock)
{
    /// <summary>Traite un lot d'événements ; renvoie leur nombre (0 : il n'y avait rien de nouveau).</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var count = 0;
        // Une transaction : les événements pris et ce qu'ils produisent sont validés ensemble, ou pas du tout
        await unitOfWork.ExecuteInTransactionAsync(async t =>
        {
            var now = clock.GetUtcNow();
            var ids = await notifications.ClaimEventsAsync(options.Value.BatchSize, now, t);
            if (ids.Count == 0) return;

            var events = await notifications.GetEventsAsync(ids, t);
            foreach (var band in events.GroupBy(e => e.BandId))
            {
                var members = await notifications.ListMembersAsync(band.Key, t);
                var modes = await notifications.GetModesAsync([.. members.Select(m => m.UserId)], t);
                NotificationMode ModeOf(string userId, NotificationKind kind)
                    => modes.TryGetValue((userId, kind), out var mode) ? mode : NotificationPreference.DefaultMode(kind);

                foreach (var e in band)
                    foreach (var planned in NotificationRules.Plan(e, members, ModeOf))
                    {
                        if (planned.Mode == NotificationMode.Immediate)
                            jobs.Enqueue(SendNotificationJob.JobType,
                                JsonSerializer.Serialize(new SendNotificationPayload(e.Id, planned.To.UserId, planned.Kind)), now);
                        else
                            notifications.AddDigestItem(NotificationDigestItem.Create(planned.To.UserId, e.Id, planned.Kind, now));
                    }
            }

            await unitOfWork.SaveChangesAsync(t);
            count = ids.Count;
        }, ct);
        return count;
    }

    /// <summary>Programme le prochain résumé quotidien s'il ne l'est pas déjà.</summary>
    public async Task EnsureDigestScheduledAsync(CancellationToken ct)
    {
        if (await jobs.HasPendingAsync(NotificationDigestJob.JobType, ct)) return;
        var o = options.Value;
        jobs.Enqueue(NotificationDigestJob.JobType, "",
            DigestSchedule.Next(clock.GetUtcNow(), o.DigestHour, TimeZoneInfo.FindSystemTimeZoneById(o.DigestTimeZone)));
        await unitOfWork.SaveChangesAsync(ct);
    }
}

/// <summary>Heure du prochain résumé quotidien.</summary>
public static class DigestSchedule
{
    /// <summary>Prochain passage à hour:00, heure locale du fuseau, strictement après now.</summary>
    public static DateTimeOffset Next(DateTimeOffset now, int hour, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var day = local.Date;
        for (var i = 0; i < 3; i++, day = day.AddDays(1))
        {
            var candidate = day.AddHours(hour);
            if (zone.IsInvalidTime(candidate)) candidate = candidate.AddHours(1);   // heure sautée au passage à l'heure d'été
            var at = new DateTimeOffset(candidate, zone.GetUtcOffset(candidate));
            if (at > now) return at.ToUniversalTime();
        }
        throw new InvalidOperationException("Impossible de calculer l'heure du résumé.");
    }
}