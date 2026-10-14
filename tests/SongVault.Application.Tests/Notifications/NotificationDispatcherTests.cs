using System.Text.Json;

using Microsoft.Extensions.Options;

using SongVault.Application.Notifications;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Notifications;

using static SongVault.Application.Tests.Notifications.NotificationTestData;

namespace SongVault.Application.Tests.Notifications;

public sealed class NotificationDispatcherTests
{
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeBackgroundJobs _jobs = new();
    private readonly FakeUnitOfWork _uow = new();

    public NotificationDispatcherTests() => _notifications.Members[BandId] = [Ana, Bo, Cy];

    private NotificationDispatcher Dispatcher(NotificationOptions? options = null)
        => new(_notifications, _jobs, _uow, Options.Create(options ?? new NotificationOptions()), new FixedTimeProvider(Now));

    [Fact]
    public async Task Immediat_une_tache_d_envoi_par_destinataire_resume_un_element()
    {
        _notifications.Events.Add(VersionAdded(id: 1));
        _notifications.Events.Add(Commented("bien", id: 2));

        var count = await Dispatcher().RunOnceAsync(CancellationToken.None);

        Assert.Equal(2, count);
        var sends = _jobs.Enqueued.Where(j => j.Type == SendNotificationJob.JobType)
            .Select(j => JsonSerializer.Deserialize<SendNotificationPayload>(j.Payload)!)
            .ToList();
        Assert.Equal([new(1, "bo", NotificationKind.NewVersion), new(1, "cy", NotificationKind.NewVersion)], sends);
        Assert.Equal(["bo", "cy"], _notifications.DigestItems.Select(i => i.UserId));
        Assert.All(_notifications.DigestItems, i => Assert.Equal((2L, NotificationKind.Comment), (i.EventId, i.Kind)));
        Assert.Equal((1, 1), (_uow.TransactionCount, _uow.SaveCount));      // tout dans une transaction
    }

    [Fact]
    public async Task Un_evenement_n_est_prepare_qu_une_fois()
    {
        _notifications.Events.Add(VersionAdded());
        var dispatcher = Dispatcher();

        await dispatcher.RunOnceAsync(CancellationToken.None);
        var second = await dispatcher.RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, second);
        Assert.Equal(2, _jobs.Enqueued.Count);
    }

    [Fact]
    public async Task Par_lots_de_BatchSize()
    {
        for (var id = 1; id <= 5; id++) _notifications.Events.Add(VersionAdded(id));

        var count = await Dispatcher(new NotificationOptions { BatchSize = 2 }).RunOnceAsync(CancellationToken.None);

        Assert.Equal(2, count);
        Assert.Equal([1L, 2L], _notifications.Notified.Order());
    }

    [Fact]
    public async Task La_preference_enregistree_remplace_le_mode_par_defaut()
    {
        _notifications.Preferences.Add(NotificationPreference.Create("bo", NotificationKind.NewVersion, NotificationMode.Daily));
        _notifications.Preferences.Add(NotificationPreference.Create("cy", NotificationKind.NewVersion, NotificationMode.Off));
        _notifications.Events.Add(VersionAdded());

        await Dispatcher().RunOnceAsync(CancellationToken.None);

        Assert.Empty(_jobs.Enqueued);
        var item = Assert.Single(_notifications.DigestItems);
        Assert.Equal("bo", item.UserId);
    }

    [Fact]
    public async Task Programme_le_resume_a_7_h_heure_de_Paris_une_seule_fois()
    {
        var dispatcher = Dispatcher();

        await dispatcher.EnsureDigestScheduledAsync(CancellationToken.None);
        await dispatcher.EnsureDigestScheduledAsync(CancellationToken.None);

        var job = Assert.Single(_jobs.Enqueued);
        Assert.Equal(NotificationDigestJob.JobType, job.Type);
        // Now = 14 octobre 9 h UTC (11 h à Paris) : prochain résumé le 15 à 7 h, heure d'été (5 h UTC)
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 5, 0, 0, TimeSpan.Zero), job.RunAfter);
    }
}