using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Api.Contracts.Releases;
using SongVault.Api.Contracts.Songs;
using SongVault.Api.Contracts.Versions;
using SongVault.Application.Abstractions;
using SongVault.Domain.Bands;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests.Releases;

[Collection(ApiCollection.Name)]
public sealed class ReleasesEndpointsTests(SongVaultApiFactory factory)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private sealed record SongRef(Guid SongId, Guid VersionId, string SongUrl);

    private static async Task<SongRef> SongAsync(HttpClient client, Guid bandId, string title)
    {
        var created = await client.PostAsJsonAsync($"/api/bands/{bandId}/songs", new { title });
        var song = (await created.Content.ReadFromJsonAsync<SongResponse>(TestJson.Options))!;
        var songUrl = created.Headers.Location!.ToString();
        var version = await client.PostAsJsonAsync($"{songUrl}/versions", new { title = "v1", status = "Final" });
        var v = (await version.Content.ReadFromJsonAsync<SongVersionResponse>(TestJson.Options))!;
        return new SongRef(song.Id, v.Id, songUrl);
    }

    private static async Task<string> ReleaseAsync(HttpClient client, Guid bandId, string title, string type = "EP", string? releaseDate = null)
    {
        var created = await client.PostAsJsonAsync($"/api/bands/{bandId}/releases", new { title, type, releaseDate });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return created.Headers.Location!.ToString();
    }

    private static Task<HttpResponseMessage> SetTracksAsync(HttpClient client, string release, params SongRef[] songs)
        => client.PutAsJsonAsync($"{release}/tracks",
            new { tracks = songs.Select(s => new { songId = s.SongId, versionId = s.VersionId }) });

    private static async Task<ReleaseResponse> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReleaseResponse>(TestJson.Options))!;
    }

    private static MultipartFormDataContent Image(byte[] bytes, string fileName) => TestFiles.Form(bytes, fileName);

    [Fact]
    public async Task Un_meme_morceau_figure_sur_un_single_puis_sur_l_album()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var aube = await SongAsync(owner, bandId, "Aube");
        var brume = await SongAsync(owner, bandId, "Brume");
        var single = await ReleaseAsync(owner, bandId, "Aube", "Single", "2026-09-01");
        var album = await ReleaseAsync(owner, bandId, "Nuits blanches", "Album", "2026-11-20");

        await ReadAsync(await SetTracksAsync(owner, single, aube));
        var detail = await ReadAsync(await SetTracksAsync(owner, album, brume, aube));

        Assert.Equal(["Brume", "Aube"], detail.Tracks.Select(t => t.SongTitle));
        Assert.Equal([1, 2], detail.Tracks.Select(t => t.TrackNumber));
        var list = (await owner.GetFromJsonAsync<List<ReleaseSummaryResponse>>($"/api/bands/{bandId}/releases", TestJson.Options))!;
        Assert.Equal(["Nuits blanches", "Aube"], list.Select(r => r.Title));     // la plus récente d'abord
        Assert.Equal([2, 1], list.Select(r => r.TrackCount));
    }

    [Fact]
    public async Task Echanger_deux_pistes_et_changer_de_version()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var aube = await SongAsync(owner, bandId, "Aube");
        var brume = await SongAsync(owner, bandId, "Brume");
        var v2 = (await (await owner.PostAsJsonAsync($"{aube.SongUrl}/versions", new { title = "v2 master", status = "Final" }))
            .Content.ReadFromJsonAsync<SongVersionResponse>(TestJson.Options))!;
        var release = await ReleaseAsync(owner, bandId, "Nuits blanches");
        await ReadAsync(await SetTracksAsync(owner, release, aube, brume));

        var swapped = await ReadAsync(await SetTracksAsync(owner, release, brume, aube with { VersionId = v2.Id }));

        Assert.Equal(["Brume", "Aube"], swapped.Tracks.Select(t => t.SongTitle));
        Assert.Equal(v2.Id, swapped.Tracks[1].VersionId);
        Assert.Equal([1, 2], swapped.Tracks[1].Versions.Select(v => v.Number));
        var reloaded = (await owner.GetFromJsonAsync<ReleaseResponse>(release, TestJson.Options))!;
        Assert.Equal(swapped.Tracks.Select(t => t.SongId), reloaded.Tracks.Select(t => t.SongId));
    }

    [Fact]
    public async Task Retirer_une_piste_renumerote_les_suivantes()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var a = await SongAsync(owner, bandId, "Aube");
        var b = await SongAsync(owner, bandId, "Brume");
        var c = await SongAsync(owner, bandId, "Crépuscule");
        var release = await ReleaseAsync(owner, bandId, "Nuits blanches");
        await ReadAsync(await SetTracksAsync(owner, release, a, b, c));

        var detail = await ReadAsync(await SetTracksAsync(owner, release, a, c));

        Assert.Equal(["Aube", "Crépuscule"], detail.Tracks.Select(t => t.SongTitle));
        Assert.Equal([1, 2], detail.Tracks.Select(t => t.TrackNumber));
    }

    [Fact]
    public async Task La_base_refuse_deux_pistes_au_meme_numero()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var a = await SongAsync(owner, bandId, "Aube");
        var b = await SongAsync(owner, bandId, "Brume");
        var release = await ReleaseAsync(owner, bandId, "Nuits blanches");
        var detail = await ReadAsync(await SetTracksAsync(owner, release, a, b));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
        var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE ReleaseTracks SET TrackNumber = 1 WHERE ReleaseId = {detail.Id} AND SongId = {b.SongId}"));
        Assert.Equal(2601, error.Number);                                // index unique (ReleaseId, TrackNumber)
    }

    [Fact]
    public async Task Les_pistes_refusent_un_doublon_et_la_version_d_un_autre_morceau()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var a = await SongAsync(owner, bandId, "Aube");
        var b = await SongAsync(owner, bandId, "Brume");
        var release = await ReleaseAsync(owner, bandId, "Nuits blanches");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SetTracksAsync(owner, release, a, a)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SetTracksAsync(owner, release, a with { VersionId = b.VersionId })).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<ReleaseResponse>(release, TestJson.Options))!.Tracks);
    }

    [Fact]
    public async Task Un_morceau_d_un_autre_groupe_ne_va_pas_sur_la_sortie()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var release = await ReleaseAsync(owner, bandId, "Nuits blanches");
        var stranger = await factory.CreateNewUserClientAsync();
        var foreign = await SongAsync(stranger, await stranger.FirstBandIdAsync(), "Ailleurs");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SetTracksAsync(owner, release, foreign)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(release)).StatusCode);   // et la sortie lui reste cachée
    }

    [Fact]
    public async Task Un_Guest_lit_les_sorties_mais_ne_les_modifie_pas()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var a = await SongAsync(owner, bandId, "Aube");
        var release = await ReleaseAsync(owner, bandId, "Nuits blanches");
        await ReadAsync(await SetTracksAsync(owner, release, a));
        var guest = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, guest, BandRole.Guest);

        Assert.Single((await guest.GetFromJsonAsync<ReleaseResponse>(release, TestJson.Options))!.Tracks);
        Assert.Equal(HttpStatusCode.Forbidden, (await SetTracksAsync(guest, release)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await guest.PostAsJsonAsync($"/api/bands/{bandId}/releases", new { title = "Non", type = "Single" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.DeleteAsync(release)).StatusCode);
    }

    [Fact]
    public async Task Un_morceau_present_sur_une_sortie_ne_se_supprime_pas()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var a = await SongAsync(owner, bandId, "Aube");
        var release = await ReleaseAsync(owner, bandId, "Nuits blanches");
        await ReadAsync(await SetTracksAsync(owner, release, a));

        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync(a.SongUrl)).StatusCode);

        await ReadAsync(await SetTracksAsync(owner, release));
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(a.SongUrl)).StatusCode);
    }

    [Fact]
    public async Task Supprimer_une_sortie_garde_ses_morceaux()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var a = await SongAsync(owner, bandId, "Aube");
        var release = await ReleaseAsync(owner, bandId, "Nuits blanches");
        await ReadAsync(await SetTracksAsync(owner, release, a));

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(release)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(release)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(a.SongUrl)).StatusCode);
    }

    [Fact]
    public async Task La_pochette_s_envoie_se_lit_et_se_retire()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var release = await ReleaseAsync(owner, bandId, "Nuits blanches");

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsync($"{release}/cover", Image(Png, "recto.png"))).StatusCode);
        var cover = await owner.GetAsync($"{release}/cover");
        Assert.Equal(HttpStatusCode.OK, cover.StatusCode);
        Assert.Equal("image/png", cover.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await cover.Content.ReadAsByteArrayAsync());
        Assert.True((await owner.GetFromJsonAsync<ReleaseResponse>(release, TestJson.Options))!.HasCover);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"{release}/cover")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"{release}/cover")).StatusCode);
    }

    [Fact]
    public async Task La_pochette_refuse_un_fichier_qui_n_est_pas_une_image()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var release = await ReleaseAsync(owner, bandId, "Nuits blanches");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PutAsync($"{release}/cover", Image(TestFiles.Mp3(1024), "pochette.jpg"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PutAsync($"{release}/cover", Image(Png, "pochette.gif"))).StatusCode);
    }

    [Fact]
    public async Task La_purge_d_un_compte_efface_les_sorties_de_son_groupe_et_leurs_pochettes()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var host = factory.WithClock(clock);
        var owner = await host.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var a = await SongAsync(owner, bandId, "Aube");
        var release = await ReleaseAsync(owner, bandId, "Nuits blanches");
        await ReadAsync(await SetTracksAsync(owner, release, a));
        await owner.PutAsync($"{release}/cover", Image(Png, "recto.png"));
        var coverKey = await CoverKeyAsync(host, bandId);

        await owner.PostAsJsonAsync("/api/me/delete", new { password = SongVaultApiFactory.TestPassword });
        clock.Advance(TimeSpan.FromDays(30));
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
        Assert.False(await db.Releases.IgnoreQueryFilters([SongVaultDbContext.BandFilter]).AnyAsync(r => r.BandId == bandId));
        Assert.False(await db.Bands.AnyAsync(b => b.Id == bandId));
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.OpenReadAsync(coverKey, CancellationToken.None));
    }

    private static async Task<string> CoverKeyAsync(WebApplicationFactory<Program> host, Guid bandId)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SongVaultDbContext>()
            .Releases.IgnoreQueryFilters([SongVaultDbContext.BandFilter])
            .Where(r => r.BandId == bandId)
            .Select(r => r.CoverStorageKey!)
            .SingleAsync();
    }
}