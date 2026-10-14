using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;

using SongVault.Api.Contracts.Activity;
using SongVault.Api.Contracts.Common;
using SongVault.Api.Contracts.Files;
using SongVault.Api.Contracts.Songs;
using SongVault.Application.Songs;
using SongVault.Domain.Activity;
using SongVault.Domain.Bands;

namespace SongVault.Api.Tests.Activity;

[Collection(ApiCollection.Name)]
public sealed class ActivityEndpointsTests(SongVaultApiFactory factory)
{
    private static async Task<string> CreateSongAsync(HttpClient client, Guid bandId, string title)
    {
        var response = await client.PostAsJsonAsync($"/api/bands/{bandId}/songs", new { title });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return response.Headers.Location!.ToString();
    }

    private static async Task<SongBadge> BadgeAsync(HttpClient client, Guid bandId, string songUrl)
    {
        var page = await client.GetFromJsonAsync<PagedResponse<SongResponse>>($"/api/bands/{bandId}/songs", TestJson.Options);
        return page!.Items.Single(s => songUrl.EndsWith(s.Id.ToString(), StringComparison.OrdinalIgnoreCase)).Badge;
    }

    private static async Task<ActivityPageResponse> ActivityAsync(HttpClient client, Guid bandId, string query = "")
        => (await client.GetFromJsonAsync<ActivityPageResponse>($"/api/bands/{bandId}/activity{query}", TestJson.Options))!;

    private async Task<(HttpClient Owner, HttpClient Member, Guid BandId)> BandWithMemberAsync(BandRole role = BandRole.Member)
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var member = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, member, role);
        return (owner, member, bandId);
    }

    [Fact]
    public async Task Badge_nouveau_puis_retire_a_l_ouverture_puis_modifie()
    {
        var (owner, member, bandId) = await BandWithMemberAsync();
        var song = await CreateSongAsync(owner, bandId, "Nocturne");

        Assert.Equal(SongBadge.New, await BadgeAsync(member, bandId, song));

        Assert.Equal(HttpStatusCode.NoContent, (await member.PostAsync($"{song}/seen", null)).StatusCode);
        Assert.Equal(SongBadge.None, await BadgeAsync(member, bandId, song));

        await owner.PostAsJsonAsync($"{song}/versions", new { title = "Démo", status = "Demo" });
        Assert.Equal(SongBadge.Updated, await BadgeAsync(member, bandId, song));
    }

    [Fact]
    public async Task Pas_de_badge_pour_ses_propres_modifications()
    {
        var (owner, _, bandId) = await BandWithMemberAsync();
        var song = await CreateSongAsync(owner, bandId, "Nocturne");
        await owner.PutAsJsonAsync(song, new { title = "Nocturne (live)" });

        Assert.Equal(SongBadge.None, await BadgeAsync(owner, bandId, song));
    }

    [Fact]
    public async Task Un_nouveau_membre_n_a_pas_de_badge_sur_l_historique()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var song = await CreateSongAsync(owner, bandId, "Nocturne");
        var newcomer = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, newcomer, BandRole.Member);

        Assert.Equal(SongBadge.None, await BadgeAsync(newcomer, bandId, song));
    }

    [Fact]
    public async Task Un_Guest_lit_le_fil_et_retire_ses_badges()
    {
        var (owner, guest, bandId) = await BandWithMemberAsync(BandRole.Guest);
        var song = await CreateSongAsync(owner, bandId, "Nocturne");
        var version = (await owner.PostAsJsonAsync($"{song}/versions", new { title = "Démo", status = "Demo" }))
            .Headers.Location!.ToString();
        var upload = await owner.PostAsync($"{version}/files", TestFiles.Form(TestFiles.Mp3(1024), "maquette.mp3"));
        var file = (await upload.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!;
        await guest.PostAsJsonAsync($"{version}/files/{file.Id}/comments", new { body = "Belle intro", positionMs = 5_000 });

        var items = (await ActivityAsync(guest, bandId)).Items;

        Assert.Equal(
            new[] { ActivityType.CommentAdded, ActivityType.FileAdded, ActivityType.VersionAdded, ActivityType.SongCreated },
            items.Select(i => i.Type));
        Assert.Equal(await guest.UserIdAsync(), items[0].ActorId);
        Assert.Equal("maquette.mp3", items[1].FileName);
        Assert.Equal(1, items[2].VersionNumber);
        Assert.All(items, i => Assert.Equal("Nocturne", i.SongTitle));
        Assert.All(items.Skip(1), i => Assert.NotNull(i.ActorEmail));
        Assert.Equal(HttpStatusCode.NoContent, (await guest.PostAsync($"{song}/seen", null)).StatusCode);
    }

    [Fact]
    public async Task Un_morceau_supprime_reste_dans_le_fil_avec_son_titre()
    {
        var (owner, member, bandId) = await BandWithMemberAsync();
        var song = await CreateSongAsync(owner, bandId, "Nocturne");
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(song)).StatusCode);

        var items = (await ActivityAsync(member, bandId)).Items;

        Assert.Equal(new[] { ActivityType.SongDeleted, ActivityType.SongCreated }, items.Select(i => i.Type));
        Assert.All(items, i => Assert.False(i.SongExists));
        Assert.All(items, i => Assert.Equal("Nocturne", i.SongTitle));
    }

    [Fact]
    public async Task La_pagination_reste_stable_quand_un_evenement_arrive()
    {
        var (owner, member, bandId) = await BandWithMemberAsync();
        for (var i = 1; i <= 5; i++) await CreateSongAsync(owner, bandId, $"Morceau {i}");

        var first = await ActivityAsync(member, bandId, "?pageSize=2");
        await CreateSongAsync(owner, bandId, "Arrivé pendant la lecture");
        var seen = first.Items.Select(i => i.SongTitle).ToList();
        var cursor = first.NextCursor;
        while (cursor is not null)
        {
            var page = await ActivityAsync(member, bandId, $"?pageSize=2&cursor={Uri.EscapeDataString(cursor)}");
            seen.AddRange(page.Items.Select(i => i.SongTitle));
            cursor = page.NextCursor;
        }

        Assert.Equal(new[] { "Morceau 5", "Morceau 4", "Morceau 3", "Morceau 2", "Morceau 1" }, seen);
    }

    [Fact]
    public async Task Un_curseur_illisible_renvoie_400()
    {
        var (_, member, bandId) = await BandWithMemberAsync();

        var response = await member.GetAsync($"/api/bands/{bandId}/activity?cursor=pas-un-curseur");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Le_fil_d_un_autre_groupe_est_introuvable()
    {
        var (_, _, bandId) = await BandWithMemberAsync();
        var stranger = await factory.CreateNewUserClientAsync();

        var response = await stranger.GetAsync($"/api/bands/{bandId}/activity");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task La_purge_garde_le_fil_des_groupes_partages_sans_auteur()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var host = factory.WithClock(clock);
        var ownerEmail = $"owner-{Guid.NewGuid():N}@test.local";
        var owner = await host.CreateNewUserClientAsync(ownerEmail);
        var bandId = await owner.FirstBandIdAsync();
        var member = await host.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, member, BandRole.Member);
        clock.Advance(TimeSpan.FromSeconds(1));                 // après l'arrivée du membre (horloge réelle)
        await CreateSongAsync(member, bandId, "Nocturne");

        var deleted = await member.PostAsJsonAsync("/api/me/delete", new { password = SongVaultApiFactory.TestPassword });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        clock.Advance(TimeSpan.FromDays(30));
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);

        // Le cookie de l'Owner a expiré pendant ces 30 jours : il se reconnecte
        owner = await LoginAsync(host, ownerEmail);
        var item = Assert.Single((await ActivityAsync(owner, bandId)).Items);
        Assert.Equal("Nocturne", item.SongTitle);
        Assert.Null(item.ActorId);
        Assert.Null(item.ActorEmail);
    }

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> host, string email)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=true",
            new { email, password = SongVaultApiFactory.TestPassword });
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Add("Cookie", login.Headers.GetValues("Set-Cookie").First().Split(';', 2)[0]);
        return client;
    }
}