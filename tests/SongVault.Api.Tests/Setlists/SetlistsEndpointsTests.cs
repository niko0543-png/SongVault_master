using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Api.Contracts.Setlists;
using SongVault.Api.Contracts.Songs;
using SongVault.Api.Contracts.Versions;
using SongVault.Domain.Bands;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests.Setlists;

/// <summary>#21 : setlists, réordonnancement et durée totale calculée par l'API.</summary>
[Collection(ApiCollection.Name)]
public sealed class SetlistsEndpointsTests(SongVaultApiFactory factory)
{
    private sealed record SongRef(Guid SongId, Guid VersionId, string SongUrl, string VersionUrl);

    private static async Task<SongRef> SongAsync(HttpClient client, Guid bandId, string title, int? bpm = null, string? key = null)
    {
        var created = await client.PostAsJsonAsync($"/api/bands/{bandId}/songs", new { title });
        var song = (await created.Content.ReadFromJsonAsync<SongResponse>(TestJson.Options))!;
        var songUrl = created.Headers.Location!.ToString();
        var version = await client.PostAsJsonAsync($"{songUrl}/versions", new { title = "v1", status = "Final", bpm, key });
        var v = (await version.Content.ReadFromJsonAsync<SongVersionResponse>(TestJson.Options))!;
        return new SongRef(song.Id, v.Id, songUrl, version.Headers.Location!.ToString());
    }

    private static async Task<string> SetlistAsync(HttpClient client, Guid bandId, string name, string? date = null, string? venue = null)
    {
        var created = await client.PostAsJsonAsync($"/api/bands/{bandId}/setlists", new { name, date, venue });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return created.Headers.Location!.ToString();
    }

    private static Task<HttpResponseMessage> SetItemsAsync(HttpClient client, string setlist, params object[] items)
        => client.PutAsJsonAsync($"{setlist}/items", new { items });

    private static object Line(SongRef song, int? durationMs = null, string? notes = null, bool withVersion = false)
        => new { songId = song.SongId, versionId = withVersion ? song.VersionId : (Guid?)null, notes, durationMs };

    private static async Task<SetlistResponse> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SetlistResponse>(TestJson.Options))!;
    }

    [Fact]
    public async Task Reordonner_les_morceaux_est_enregistre_dans_le_nouvel_ordre()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var aube = await SongAsync(owner, bandId, "Aube");
        var brume = await SongAsync(owner, bandId, "Brume");
        var cendres = await SongAsync(owner, bandId, "Cendres");
        var setlist = await SetlistAsync(owner, bandId, "Concert au Ferrailleur", "2026-11-28", "Nantes");
        await ReadAsync(await SetItemsAsync(owner, setlist, Line(aube), Line(brume), Line(cendres)));

        // « Cendres » passe en tête, puis « Aube » et « Brume » échangent leurs places
        var moved = await ReadAsync(await SetItemsAsync(owner, setlist, Line(cendres), Line(brume), Line(aube, notes: "enchaîner")));

        Assert.Equal(["Cendres", "Brume", "Aube"], moved.Items.Select(i => i.SongTitle));
        Assert.Equal([1, 2, 3], moved.Items.Select(i => i.Position));
        var reloaded = (await owner.GetFromJsonAsync<SetlistResponse>(setlist, TestJson.Options))!;
        Assert.Equal(moved.Items.Select(i => i.SongId), reloaded.Items.Select(i => i.SongId));
        Assert.Equal("enchaîner", reloaded.Items[2].Notes);
        var list = (await owner.GetFromJsonAsync<List<SetlistSummaryResponse>>($"/api/bands/{bandId}/setlists", TestJson.Options))!;
        Assert.Equal(("Concert au Ferrailleur", "Nantes", 3), (list.Single().Name, list.Single().Venue, list.Single().ItemCount));
    }

    [Fact]
    public async Task Le_total_est_exact_avec_la_duree_audio_et_les_durees_saisies()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var aube = await SongAsync(owner, bandId, "Aube", 92, "Am");
        var brume = await SongAsync(owner, bandId, "Brume");
        var cendres = await SongAsync(owner, bandId, "Cendres");
        var upload = await owner.PostAsync($"{aube.VersionUrl}/files", TestFiles.Form(TestFiles.Wav(2), "aube.wav"));
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);     // #17 analyse, #15 durée
        var setlist = await SetlistAsync(owner, bandId, "Répétition");

        var detail = await ReadAsync(await SetItemsAsync(owner, setlist,
            Line(aube), Line(brume, durationMs: 215_000), Line(cendres, durationMs: 183_500)));

        var audio = detail.Items[0].AudioDurationMs!.Value;
        Assert.InRange(audio, 1990, 2010);                                // le WAV de test dure 2 s
        Assert.Equal(("Am", 92), (detail.Items[0].Key, detail.Items[0].Bpm));
        Assert.Equal(audio + 215_000L + 183_500L, detail.TotalDurationMs);
        Assert.Equal(0, detail.MissingDurationCount);

        // Une durée saisie prime sur celle du fichier ; une ligne sans durée est comptée à part
        var again = await ReadAsync(await SetItemsAsync(owner, setlist, Line(aube, durationMs: 200_000), Line(brume), Line(cendres)));
        Assert.Equal(200_000L, again.TotalDurationMs);
        Assert.Equal(2, again.MissingDurationCount);
    }

    [Fact]
    public async Task Les_lignes_refusent_doublon_duree_hors_limites_et_version_d_un_autre_morceau()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var a = await SongAsync(owner, bandId, "Aube");
        var b = await SongAsync(owner, bandId, "Brume");
        var setlist = await SetlistAsync(owner, bandId, "Concert");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SetItemsAsync(owner, setlist, Line(a), Line(a))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SetItemsAsync(owner, setlist, Line(a, durationMs: 500))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SetItemsAsync(owner, setlist, new { songId = a.SongId, versionId = b.VersionId })).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<SetlistResponse>(setlist, TestJson.Options))!.Items);
    }

    [Fact]
    public async Task Un_morceau_d_un_autre_groupe_ne_va_pas_dans_la_setlist()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var setlist = await SetlistAsync(owner, bandId, "Concert");
        var stranger = await factory.CreateNewUserClientAsync();
        var foreign = await SongAsync(stranger, await stranger.FirstBandIdAsync(), "Ailleurs");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SetItemsAsync(owner, setlist, Line(foreign))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(setlist)).StatusCode);
    }

    [Fact]
    public async Task Un_Guest_lit_les_setlists_mais_ne_les_modifie_pas()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var a = await SongAsync(owner, bandId, "Aube");
        var setlist = await SetlistAsync(owner, bandId, "Concert");
        await ReadAsync(await SetItemsAsync(owner, setlist, Line(a)));
        var guest = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, guest, BandRole.Guest);

        Assert.Single((await guest.GetFromJsonAsync<SetlistResponse>(setlist, TestJson.Options))!.Items);
        Assert.Equal(HttpStatusCode.Forbidden, (await SetItemsAsync(guest, setlist)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await guest.PostAsJsonAsync($"/api/bands/{bandId}/setlists", new { name = "Non" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.DeleteAsync(setlist)).StatusCode);
    }

    [Fact]
    public async Task Un_morceau_d_une_setlist_ne_va_pas_a_la_corbeille()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var a = await SongAsync(owner, bandId, "Aube");
        var setlist = await SetlistAsync(owner, bandId, "Concert");
        await ReadAsync(await SetItemsAsync(owner, setlist, Line(a, withVersion: true)));

        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync(a.VersionUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync(a.SongUrl)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(setlist)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(setlist)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(a.SongUrl)).StatusCode);
    }

    [Fact]
    public async Task La_purge_d_un_compte_efface_les_setlists_de_son_groupe()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var host = factory.WithClock(clock);
        var owner = await host.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var a = await SongAsync(owner, bandId, "Aube");
        var setlist = await SetlistAsync(owner, bandId, "Concert");
        await ReadAsync(await SetItemsAsync(owner, setlist, Line(a)));

        await owner.PostAsJsonAsync("/api/me/delete", new { password = SongVaultApiFactory.TestPassword });
        clock.Advance(TimeSpan.FromDays(30));
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
        Assert.False(await db.Setlists.IgnoreQueryFilters([SongVaultDbContext.BandFilter]).AnyAsync(s => s.BandId == bandId));
        Assert.False(await db.Bands.AnyAsync(b => b.Id == bandId));
    }
}