using SongVault.Domain.Common;
using SongVault.Domain.Notifications;

namespace SongVault.Domain.Tests.Notifications;

public sealed class NotificationPreferenceTests
{
    [Fact]
    public void Modes_par_defaut_versions_et_mentions_tout_de_suite_commentaires_au_resume()
    {
        Assert.Equal(NotificationMode.Immediate, NotificationPreference.DefaultMode(NotificationKind.NewVersion));
        Assert.Equal(NotificationMode.Daily, NotificationPreference.DefaultMode(NotificationKind.Comment));
        Assert.Equal(NotificationMode.Immediate, NotificationPreference.DefaultMode(NotificationKind.Mention));
    }

    [Fact]
    public void Create_puis_ChangeTo()
    {
        var preference = NotificationPreference.Create("ana", NotificationKind.Comment, NotificationMode.Off);

        preference.ChangeTo(NotificationMode.Immediate);

        Assert.Equal("ana", preference.UserId);
        Assert.Equal(NotificationKind.Comment, preference.Kind);
        Assert.Equal(NotificationMode.Immediate, preference.Mode);
    }

    [Fact]
    public void Refuse_un_mode_ou_un_type_inconnu()
    {
        Assert.Throws<DomainException>(() => NotificationPreference.Create("ana", (NotificationKind)99, NotificationMode.Off));
        Assert.Throws<DomainException>(() => NotificationPreference.Create("ana", NotificationKind.Comment, (NotificationMode)99));
        Assert.Throws<DomainException>(() => NotificationPreference.Create(" ", NotificationKind.Comment, NotificationMode.Off));
    }

    [Fact]
    public void Un_element_de_resume_raconte_un_evenement()
    {
        var now = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

        var item = NotificationDigestItem.Create("ana", 42, NotificationKind.Comment, now);

        Assert.Equal(42, item.EventId);
        Assert.Throws<DomainException>(() => NotificationDigestItem.Create("ana", 0, NotificationKind.Comment, now));
    }
}