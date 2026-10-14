using System.Text.Json;

using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Application.Common;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Notifications;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Notifications;

using static SongVault.Application.Tests.Notifications.NotificationTestData;

namespace SongVault.Application.Tests.Notifications;

public sealed class NotificationJobsTests
{
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeEmailSender _emails = new();
    private static readonly IOptions<AppOptions> App = Options.Create(new AppOptions { PublicUrl = "http://localhost:8080/" });

    public NotificationJobsTests() => _notifications.Members[BandId] = [Ana, Bo, Cy];

    private SendNotificationJob Send() => new(_notifications, _emails, new FakeUnsubscribeTokens(), App);
    private NotificationDigestJob Digest() => new(_notifications, _emails, new FakeUnsubscribeTokens(), App);

    private static BackgroundJobContext Job(string payload) => new(Guid.NewGuid(), payload, 1, false);
    private static string Payload(long eventId, string userId, NotificationKind kind)
        => JsonSerializer.Serialize(new SendNotificationPayload(eventId, userId, kind));

    [Fact]
    public async Task Envoie_l_e_mail_avec_le_lien_de_la_version_et_la_desinscription()
    {
        _notifications.Events.Add(VersionAdded(id: 7));

        await Send().RunAsync(Job(Payload(7, "bo", NotificationKind.NewVersion)), CancellationToken.None);

        var mail = Assert.Single(_emails.Sent);
        Assert.Equal("bo@test.local", mail.To);
        Assert.Equal("« Nocturne » : nouvelle version v4", mail.Subject);
        Assert.Contains("ana@test.local a ajouté la v4 « Démo » de « Nocturne ».", mail.TextBody);
        Assert.Contains($"http://localhost:8080/b/{BandId}/songs/{SongId}/versions/{VersionId}", mail.TextBody);
        Assert.Contains("http://localhost:8080/unsubscribe?token=bo%7CNewVersion", mail.TextBody);
        Assert.Equal("<http://localhost:8080/api/notifications/unsubscribe?token=bo%7CNewVersion>",
            mail.Headers!["List-Unsubscribe"]);
        Assert.Equal("List-Unsubscribe=One-Click", mail.Headers["List-Unsubscribe-Post"]);
    }

    [Fact]
    public async Task Rien_si_le_destinataire_a_quitte_le_groupe_ou_change_d_avis()
    {
        _notifications.Events.Add(VersionAdded(id: 7));
        _notifications.Members[BandId] = [Ana, Bo];                                // cy est parti
        _notifications.Preferences.Add(NotificationPreference.Create("bo", NotificationKind.NewVersion, NotificationMode.Off));

        await Send().RunAsync(Job(Payload(7, "bo", NotificationKind.NewVersion)), CancellationToken.None);
        await Send().RunAsync(Job(Payload(7, "cy", NotificationKind.NewVersion)), CancellationToken.None);
        await Send().RunAsync(Job(Payload(99, "bo", NotificationKind.NewVersion)), CancellationToken.None);  // groupe effacé

        Assert.Empty(_emails.Sent);
    }

    [Fact]
    public async Task Un_refus_du_fournisseur_fait_reessayer_la_tache()
    {
        _notifications.Events.Add(VersionAdded(id: 7));
        _emails.FailFor.Add("bo@test.local");

        await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            Send().RunAsync(Job(Payload(7, "bo", NotificationKind.NewVersion)), CancellationToken.None));
    }

    [Fact]
    public async Task Une_mention_cite_le_commentaire()
    {
        _notifications.Events.Add(Commented("@bo@test.local   le refrain\narrive trop tôt", id: 8));

        await Send().RunAsync(Job(Payload(8, "bo", NotificationKind.Mention)), CancellationToken.None);

        var mail = Assert.Single(_emails.Sent);
        Assert.Equal("ana@test.local vous a mentionné sur « Nocturne »", mail.Subject);
        Assert.Contains("ana@test.local vous a mentionné dans un commentaire sur prise-2.mp3 (v4 de « Nocturne »).", mail.TextBody);
        Assert.Contains("« @bo@test.local le refrain arrive trop tôt »", mail.TextBody);
    }

    [Fact]
    public async Task Le_resume_regroupe_par_groupe_puis_efface_les_elements()
    {
        var other = Guid.NewGuid();
        _notifications.Members[other] = [Ana, Bo];
        _notifications.Events.Add(Commented("un", id: 1));
        _notifications.Events.Add(VersionAdded(id: 2) with { BandId = other, BandName = "Duo" });
        _notifications.AddDigestItem(NotificationDigestItem.Create("bo", 1, NotificationKind.Comment, Now));
        _notifications.Preferences.Add(NotificationPreference.Create("bo", NotificationKind.NewVersion, NotificationMode.Daily));
        _notifications.AddDigestItem(NotificationDigestItem.Create("bo", 2, NotificationKind.NewVersion, Now));

        await Digest().RunAsync(Job(""), CancellationToken.None);

        var mail = Assert.Single(_emails.Sent);
        Assert.Equal("SongVault : 2 nouveautés dans vos groupes", mail.Subject);
        var text = mail.TextBody;
        Assert.True(text.IndexOf("Groupe « Les Lunes »", StringComparison.Ordinal)
                    < text.IndexOf("Groupe « Duo »", StringComparison.Ordinal));
        Assert.Contains("- ana@test.local a commenté prise-2.mp3 (v4 de « Nocturne ») : http://localhost:8080/b/", text);
        Assert.Contains("token=bo%7C%2A", text);                                  // désinscription de tout
        Assert.Empty(_notifications.Digest);
    }

    [Fact]
    public async Task Le_resume_oublie_les_groupes_quittes_et_les_types_qui_ne_sont_plus_au_resume()
    {
        _notifications.Events.Add(Commented("un", id: 1));
        _notifications.AddDigestItem(NotificationDigestItem.Create("cy", 1, NotificationKind.Comment, Now));
        _notifications.AddDigestItem(NotificationDigestItem.Create("bo", 1, NotificationKind.Comment, Now));
        _notifications.Members[BandId] = [Ana, Bo];                                // cy est parti
        _notifications.Preferences.Add(NotificationPreference.Create("bo", NotificationKind.Comment, NotificationMode.Immediate));

        await Digest().RunAsync(Job(""), CancellationToken.None);

        Assert.Empty(_emails.Sent);
        Assert.Empty(_notifications.Digest);
    }

    [Fact]
    public async Task Un_resume_refuse_garde_ses_elements_et_ceux_deja_envoyes_ne_repartent_pas()
    {
        _notifications.Events.Add(Commented("un", id: 1));
        _notifications.AddDigestItem(NotificationDigestItem.Create("bo", 1, NotificationKind.Comment, Now));
        _notifications.AddDigestItem(NotificationDigestItem.Create("cy", 1, NotificationKind.Comment, Now));
        _emails.FailFor.Add("cy@test.local");

        await Assert.ThrowsAsync<EmailDeliveryException>(() => Digest().RunAsync(Job(""), CancellationToken.None));
        _emails.FailFor.Clear();
        await Digest().RunAsync(Job(""), CancellationToken.None);                  // nouvel essai de la tâche

        Assert.Equal(["bo@test.local", "cy@test.local"], _emails.Sent.Select(m => m.To));
    }
}