using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Api.Contracts.Files;
using SongVault.Api.Contracts.Notifications;
using SongVault.Application.Abstractions;
using SongVault.Application.Notifications;
using SongVault.Domain.Bands;
using SongVault.Domain.Notifications;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests.Notifications;

[Collection(ApiCollection.Name)]
public sealed class NotificationsEndpointsTests(SongVaultApiFactory factory)
{
    private const string Preferences = "/api/me/notifications";

    /// <summary>Un groupe : l'Owner, un Member avec son adresse, et une version avec un MP3 à commenter.</summary>
    private sealed record Arranged(
        HttpClient Owner, string OwnerEmail, HttpClient Member, string MemberEmail, Guid BandId,
        string Song, string Comments);

    private static async Task<Arranged> ArrangeAsync(WebApplicationFactory<Program> host)
    {
        var ownerEmail = $"owner-{Guid.NewGuid():N}@test.local";
        var memberEmail = $"member-{Guid.NewGuid():N}@test.local";
        var owner = await host.CreateNewUserClientAsync(ownerEmail);
        var bandId = await owner.FirstBandIdAsync();
        var member = await host.CreateNewUserClientAsync(memberEmail);
        await AddMemberAsync(host, bandId, member);

        var song = (await owner.PostAsJsonAsync($"/api/bands/{bandId}/songs", new { title = "Nocturne" }))
            .Headers.Location!.ToString();
        var version = (await owner.PostAsJsonAsync($"{song}/versions", new { title = "Démo", status = "Demo" }))
            .Headers.Location!.ToString();
        var upload = await owner.PostAsync($"{version}/files", TestFiles.Form(TestFiles.Mp3(1024), "maquette.mp3"));
        var file = (await upload.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!;
        return new Arranged(owner, ownerEmail, member, memberEmail, bandId, song, $"{version}/files/{file.Id}/comments");
    }

    /// <summary>Comme factory.AddMemberAsync, mais sur un hôte dérivé (horloge de test).</summary>
    private static async Task AddMemberAsync(WebApplicationFactory<Program> host, Guid bandId, HttpClient member)
    {
        var userId = await member.UserIdAsync();
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
        var band = await db.Bands.Include(b => b.Memberships).SingleAsync(b => b.Id == bandId);
        band.AddMember(userId, BandRole.Member, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Notifications reçues par cette adresse (#12), de la plus ancienne à la plus récente : celles qui portent
    /// List-Unsubscribe. L'e-mail de confirmation du compte (#4) part aussi à chaque adresse de test.
    /// </summary>
    private IReadOnlyList<EmailMessage> NotificationsTo(string email)
        => [.. factory.Emails.AllTo(email).Where(m => m.Headers?.ContainsKey("List-Unsubscribe") == true)];

    private static async Task<List<NotificationPreferenceResponse>> PreferencesAsync(HttpClient client)
        => (await client.GetFromJsonAsync<List<NotificationPreferenceResponse>>(Preferences, TestJson.Options))!;

    private static async Task SetAsync(HttpClient client, NotificationKind kind, NotificationMode mode)
        => Assert.Equal(HttpStatusCode.OK,
            (await client.PutAsJsonAsync(Preferences, new[] { new { kind = kind.ToString(), mode = mode.ToString() } })).StatusCode);

    private static async Task AddVersionAsync(HttpClient client, string song)
        => Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync($"{song}/versions", new { title = "Mix 2", status = "Demo" })).StatusCode);

    private static async Task CommentAsync(HttpClient client, string comments, string body)
        => Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(comments, new { body })).StatusCode);

    [Fact]
    public async Task Preferences_par_defaut_puis_modifiees()
    {
        var client = await factory.CreateNewUserClientAsync();

        var defaults = await PreferencesAsync(client);
        Assert.Equal(
            [new(NotificationKind.NewVersion, NotificationMode.Immediate, true),
             new(NotificationKind.Comment, NotificationMode.Daily, true),
             new NotificationPreferenceResponse(NotificationKind.Mention, NotificationMode.Immediate, true)],
            defaults);

        await SetAsync(client, NotificationKind.Comment, NotificationMode.Off);

        Assert.Contains(new NotificationPreferenceResponse(NotificationKind.Comment, NotificationMode.Off, false),
            await PreferencesAsync(client));
    }

    [Theory]
    [InlineData("""[{"kind":"Comment","mode":"Off"},{"kind":"Comment","mode":"Daily"}]""")]
    [InlineData("""[{"kind":"Comment","mode":"Weekly"}]""")]
    [InlineData("""[{"kind":"Comment"}]""")]
    public async Task Preferences_invalides_400(string json)
    {
        var client = await factory.CreateNewUserClientAsync();

        var response = await client.PutAsync(Preferences, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Nouvelle_version_un_e_mail_aux_autres_membres_apres_la_requete()
    {
        var a = await ArrangeAsync(factory);
        await SongVaultApiFactory.RunNotificationsAsync(factory.Services);            // la version « Démo »
        var before = NotificationsTo(a.MemberEmail).Count;

        await AddVersionAsync(a.Owner, a.Song);
        Assert.Equal(before, NotificationsTo(a.MemberEmail).Count);
        await SongVaultApiFactory.RunNotificationsAsync(factory.Services);

        var mail = NotificationsTo(a.MemberEmail)[^1];
        Assert.Equal("« Nocturne » : nouvelle version v2", mail.Subject);
        Assert.Contains($"{a.OwnerEmail} a ajouté la v2 « Mix 2 » de « Nocturne ».", mail.TextBody);
        Assert.Contains($"http://localhost:8080/b/{a.BandId}/songs/", mail.TextBody);
        Assert.StartsWith("<http://localhost:8080/api/notifications/unsubscribe?token=", mail.Headers!["List-Unsubscribe"]);
        Assert.Empty(NotificationsTo(a.OwnerEmail));                             // jamais pour ses propres modifications
    }

    [Fact]
    public async Task Type_desactive_aucun_e_mail()
    {
        var a = await ArrangeAsync(factory);
        await SetAsync(a.Member, NotificationKind.NewVersion, NotificationMode.Off);

        await AddVersionAsync(a.Owner, a.Song);
        await SongVaultApiFactory.RunNotificationsAsync(factory.Services);

        Assert.Empty(NotificationsTo(a.MemberEmail));
    }

    [Fact]
    public async Task Une_mention_part_tout_de_suite()
    {
        var a = await ArrangeAsync(factory);
        await SetAsync(a.Member, NotificationKind.NewVersion, NotificationMode.Off);

        await CommentAsync(a.Owner, a.Comments, $"@{a.MemberEmail} le refrain arrive trop tôt");
        await SongVaultApiFactory.RunNotificationsAsync(factory.Services);

        var mail = NotificationsTo(a.MemberEmail)[^1];
        Assert.Equal($"{a.OwnerEmail} vous a mentionné sur « Nocturne »", mail.Subject);
        Assert.Contains("le refrain arrive trop tôt", mail.TextBody);
    }

    [Fact]
    public async Task Le_lien_de_desinscription_marche_sans_connexion()
    {
        var a = await ArrangeAsync(factory);
        await AddVersionAsync(a.Owner, a.Song);
        await SongVaultApiFactory.RunNotificationsAsync(factory.Services);
        var header = NotificationsTo(a.MemberEmail)[^1].Headers!["List-Unsubscribe"];
        var url = header.Trim('<', '>').Replace("http://localhost:8080", "", StringComparison.Ordinal);
        var anonymous = factory.CreateClient();

        // Désinscription en un clic (RFC 8058) : la messagerie poste ce formulaire
        var response = await anonymous.PostAsync(url,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["List-Unsubscribe"] = "One-Click" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UnsubscribeResponse>(TestJson.Options);
        Assert.Equal(NotificationKind.NewVersion, body!.Kind);
        Assert.Contains(new NotificationPreferenceResponse(NotificationKind.NewVersion, NotificationMode.Off, false),
            await PreferencesAsync(a.Member));
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsync(url, null)).StatusCode);      // deux fois : sans effet
    }

    [Theory]
    [InlineData("")]
    [InlineData("?token=abc")]
    public async Task Jeton_absent_ou_invalide_400(string query)
    {
        var response = await factory.CreateClient().PostAsync($"/api/notifications/unsubscribe{query}", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Les_commentaires_partent_dans_le_resume_du_matin()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var host = factory.WithClock(clock);
        var a = await ArrangeAsync(host);

        await CommentAsync(a.Owner, a.Comments, "Bon mix dans l'ensemble");
        await SongVaultApiFactory.RunNotificationsAsync(host.Services);
        // Commentaires : résumé par défaut. Seule la version « Démo » d'ArrangeAsync est partie tout de suite
        Assert.DoesNotContain(NotificationsTo(a.MemberEmail), m => m.Subject.Contains("commentaire", StringComparison.Ordinal));

        using (var scope = host.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<NotificationDispatcher>().EnsureDigestScheduledAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromHours(25));                                          // le prochain 7 h est passé
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);

        var mail = NotificationsTo(a.MemberEmail)[^1];
        Assert.Equal("SongVault : 1 nouveauté dans vos groupes", mail.Subject);
        Assert.Contains($"{a.OwnerEmail} a commenté maquette.mp3 (v1 de « Nocturne »)", mail.TextBody);
        Assert.Contains("List-Unsubscribe", mail.Headers!.Keys);
    }
}