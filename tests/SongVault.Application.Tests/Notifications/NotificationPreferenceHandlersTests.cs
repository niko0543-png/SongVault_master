using SongVault.Application.Notifications;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Notifications;

namespace SongVault.Application.Tests.Notifications;

public sealed class NotificationPreferenceHandlersTests
{
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentUser _user = new("bo", "bo@test.local");

    [Fact]
    public async Task Sans_choix_les_modes_par_defaut()
    {
        var prefs = await new GetNotificationPreferencesHandler(_notifications, _user).HandleAsync(CancellationToken.None);

        Assert.Equal(
            [new(NotificationKind.NewVersion, NotificationMode.Immediate, true),
             new(NotificationKind.Comment, NotificationMode.Daily, true),
             new NotificationPreferenceDto(NotificationKind.Mention, NotificationMode.Immediate, true)],
            prefs);
    }

    [Fact]
    public async Task Modifier_cree_puis_change_la_preference()
    {
        var handler = new UpdateNotificationPreferencesHandler(_notifications, _uow, _user);

        await handler.HandleAsync(new Dictionary<NotificationKind, NotificationMode>
        { [NotificationKind.Comment] = NotificationMode.Off }, CancellationToken.None);
        var prefs = await handler.HandleAsync(new Dictionary<NotificationKind, NotificationMode>
        { [NotificationKind.Comment] = NotificationMode.Immediate }, CancellationToken.None);

        var saved = Assert.Single(_notifications.Preferences);
        Assert.Equal(("bo", NotificationKind.Comment, NotificationMode.Immediate), (saved.UserId, saved.Kind, saved.Mode));
        Assert.Contains(new NotificationPreferenceDto(NotificationKind.Comment, NotificationMode.Immediate, false), prefs);
        Assert.Equal(2, _uow.SaveCount);
    }

    [Fact]
    public async Task Le_lien_d_un_e_mail_coupe_son_type()
    {
        var result = await Unsubscribe("bo|Mention");

        Assert.Equal(NotificationKind.Mention, result!.Kind);
        var saved = Assert.Single(_notifications.Preferences);
        Assert.Equal((NotificationKind.Mention, NotificationMode.Off), (saved.Kind, saved.Mode));
    }

    [Fact]
    public async Task Le_lien_du_resume_coupe_tout()
    {
        _notifications.Preferences.Add(NotificationPreference.Create("bo", NotificationKind.Comment, NotificationMode.Daily));

        var result = await Unsubscribe("bo|*");

        Assert.Null(result!.Kind);
        Assert.Equal(3, _notifications.Preferences.Count);
        Assert.All(_notifications.Preferences, p => Assert.Equal(NotificationMode.Off, p.Mode));
    }

    [Fact]
    public async Task Jeton_invalide_null_compte_supprime_sans_effet()
    {
        _notifications.DeletedUsers.Add("zed");

        Assert.Null(await Unsubscribe("n'importe quoi"));
        Assert.NotNull(await Unsubscribe("zed|Comment"));
        Assert.Empty(_notifications.Preferences);
    }

    private Task<UnsubscribeResult?> Unsubscribe(string token)
        => new UnsubscribeHandler(_notifications, new FakeUnsubscribeTokens(), _uow).HandleAsync(token, CancellationToken.None);
}