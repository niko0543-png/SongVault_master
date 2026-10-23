using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Api.Contracts.Common;
using SongVault.Api.Contracts.Files;
using SongVault.Api.Contracts.Releases;
using SongVault.Api.Contracts.Songs;
using SongVault.Api.Contracts.Trash;
using SongVault.Api.Contracts.Versions;
using SongVault.Application.Abstractions;
using SongVault.Domain.Bands;
using SongVault.Domain.Releases;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests.Releases;

/// <summary>#38 : les infos de sortie de la dernière version envoient le morceau dans l'onglet Sorties.</summary>
[Collection(ApiCollection.Name)]
public sealed class SongReleaseEndpointsTests(SongVaultApiFactory factory)
{
    private sealed record Created(Guid BandId, string SongUrl, Guid SongId, Guid V1, Guid V2, string V1Key);

    /// <summary>Nocturne : v1 maquette avec un MP3 (analysé), v2 Final sans fichier.</summary>
    private async Task<Created> NocturneAsync(HttpClient client, Guid? band = null)
    {
        var bandId = band ?? await client.FirstBandIdAsync();
        var song = await client.PostAsJsonAsync($"/api/bands/{bandId}/songs", new { title = "Nocturne" });
        var songId = (await song.Content.ReadFromJsonAsync<SongResponse>(TestJson.Options))!.Id;
        var songUrl = song.Headers.Location!.ToString();
        var v1 = await client.PostAsJsonAsync($"{songUrl}/versions", new { title = "Maquette", status = "Demo" });
        var v1Id = (await v1.Content.ReadFromJsonAsync<SongVersionResponse>(TestJson.Options))!.Id;
        var upload = await client.PostAsync($"{v1.Headers.Location}/files", TestFiles.Form(TestFiles.Mp3(4096), "prise.mp3"));
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var fileId = (await upload.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!.Id;
        var v2 = await client.PostAsJsonAsync($"{songUrl}/versions", new { title = "Mix final", status = "Final" });
        var v2Id = (await v2.Content.ReadFromJsonAsync<SongVersionResponse>(TestJson.Options))!.Id;
        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);

        using var scope = factory.Services.CreateScope();
        var key = await scope.ServiceProvider.GetRequiredService<SongVaultDbContext>().Set<Domain.Files.SongFile>()
            .IgnoreQueryFilters().Where(f => f.Id == fileId).Select(f => f.StorageKey).SingleAsync();
        return new Created(bandId, songUrl, songId, v1Id, v2Id, key);
    }

    private static object Body(Guid versionId, string keep = "All", bool purgeNow = false, Guid? releaseId = null) => new
    {
        versionId,
        releaseId,
        releaseDate = "2026-11-20",
        label = "Nuit Records",
        upc = "0 36000 29145 2",
        streamingLinks = new[] { "https://open.spotify.com/track/1", "https://lesnuits.bandcamp.com/track/nocturne" },
        keep,
        purgeNow,
    };

    private static async Task<List<SongResponse>> SongsAsync(HttpClient client, Guid bandId, bool? released)
        => [.. (await client.GetFromJsonAsync<PagedResponse<SongResponse>>(
            $"/api/bands/{bandId}/songs{(released is null ? "" : $"?released={released.Value.ToString().ToLowerInvariant()}")}",
            TestJson.Options))!.Items];

    [Fact]
    public async Task Le_morceau_sort_en_single_et_passe_dans_les_sorties()
    {
        var client = await factory.CreateNewUserClientAsync();
        var c = await NocturneAsync(client);

        var response = await client.PostAsJsonAsync($"{c.SongUrl}/release", Body(c.V2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<SongReleaseResponse>(TestJson.Options))!;
        Assert.Empty(result.RemovedVersions);
        Assert.Empty(await SongsAsync(client, c.BandId, released: false));
        var released = Assert.Single(await SongsAsync(client, c.BandId, released: true));
        Assert.Equal(result.ReleaseId, released.ReleaseId);
        Assert.NotNull(released.ReleasedAt);
        Assert.Single(await SongsAsync(client, c.BandId, released: null));

        var release = (await client.GetFromJsonAsync<ReleaseResponse>($"/api/bands/{c.BandId}/releases/{result.ReleaseId}", TestJson.Options))!;
        Assert.Equal(("Nocturne", ReleaseType.Single, new DateOnly(2026, 11, 20)), (release.Title, release.Type, release.ReleaseDate));
        Assert.Equal(("Nuit Records", "036000291452"), (release.Label, release.Upc));
        Assert.Equal([StreamingPlatform.Spotify, StreamingPlatform.Bandcamp], release.StreamingLinks.Select(l => l.Platform));
        var track = Assert.Single(release.Tracks);
        Assert.Equal((c.V2, true), (track.VersionId, track.IsReleasedHere));

        // Déjà sorti : 409
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{c.SongUrl}/release", Body(c.V2))).StatusCode);
    }

    [Fact]
    public async Task Ne_garder_que_la_derniere_et_liberer_la_place_tout_de_suite()
    {
        var client = await factory.CreateNewUserClientAsync();
        var c = await NocturneAsync(client);
        var storage = factory.Services.GetRequiredService<IFileStorageService>();
        Assert.NotNull(await storage.GetLengthAsync(c.V1Key, CancellationToken.None));

        var response = await client.PostAsJsonAsync($"{c.SongUrl}/release", Body(c.V2, "LatestOnly", purgeNow: true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<SongReleaseResponse>(TestJson.Options))!;
        Assert.Equal([1], result.RemovedVersions);
        Assert.True(result.Purged);
        Assert.Null(await storage.GetLengthAsync(c.V1Key, CancellationToken.None));
        Assert.Empty((await client.GetFromJsonAsync<List<TrashItemResponse>>($"/api/bands/{c.BandId}/trash", TestJson.Options))!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{c.SongUrl}/versions/{c.V1}")).StatusCode);
    }

    [Fact]
    public async Task Sans_effacer_les_anciennes_versions_vont_a_la_corbeille()
    {
        var client = await factory.CreateNewUserClientAsync();
        var c = await NocturneAsync(client);

        await client.PostAsJsonAsync($"{c.SongUrl}/release", Body(c.V2, "LatestOnly"));

        var item = Assert.Single((await client.GetFromJsonAsync<List<TrashItemResponse>>($"/api/bands/{c.BandId}/trash", TestJson.Options))!);
        Assert.Equal(c.V1, item.Id);
    }

    [Fact]
    public async Task Un_membre_ne_peut_pas_effacer_sans_passer_par_la_corbeille()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var member = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, member, BandRole.Member);
        var c = await NocturneAsync(owner, bandId);

        var refused = await member.PostAsJsonAsync($"{c.SongUrl}/release", Body(c.V2, "LatestOnly", purgeNow: true));
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.PostAsJsonAsync($"{c.SongUrl}/release", Body(c.V2, "LatestOnly"))).StatusCode);
    }

    [Fact]
    public async Task Une_version_qui_n_est_pas_la_derniere_finale_est_refusee()
    {
        var client = await factory.CreateNewUserClientAsync();
        var c = await NocturneAsync(client);

        var response = await client.PostAsJsonAsync($"{c.SongUrl}/release", Body(c.V1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("v2", await response.Content.ReadAsStringAsync());
        Assert.Single(await SongsAsync(client, c.BandId, released: false));
    }

    [Fact]
    public async Task Supprimer_la_sortie_ou_remettre_en_travail_ramene_le_morceau_dans_la_liste()
    {
        var client = await factory.CreateNewUserClientAsync();
        var c = await NocturneAsync(client);
        var result = (await (await client.PostAsJsonAsync($"{c.SongUrl}/release", Body(c.V2)))
            .Content.ReadFromJsonAsync<SongReleaseResponse>(TestJson.Options))!;

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{c.SongUrl}/release")).StatusCode);
        Assert.Single(await SongsAsync(client, c.BandId, released: false));
        Assert.Single((await client.GetFromJsonAsync<ReleaseResponse>($"/api/bands/{c.BandId}/releases/{result.ReleaseId}", TestJson.Options))!.Tracks);

        // Ressortie sur la même sortie, puis la sortie supprimée : SET NULL remet le morceau en travail
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"{c.SongUrl}/release", Body(c.V2, releaseId: result.ReleaseId))).StatusCode);
        Assert.Empty(await SongsAsync(client, c.BandId, released: false));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/bands/{c.BandId}/releases/{result.ReleaseId}")).StatusCode);
        var song = Assert.Single(await SongsAsync(client, c.BandId, released: false));
        Assert.Null(song.ReleaseId);
        Assert.Null(song.ReleasedAt);
    }

    [Fact]
    public async Task Les_liens_d_ecoute_d_une_sortie_se_modifient_et_sont_verifies()
    {
        var client = await factory.CreateNewUserClientAsync();
        var bandId = await client.FirstBandIdAsync();
        var created = await client.PostAsJsonAsync($"/api/bands/{bandId}/releases", new { title = "Nuits blanches", type = "EP" });
        var url = created.Headers.Location!.ToString();

        var saved = await client.PutAsJsonAsync($"{url}/distribution",
            new { label = "Nuit Records", upc = "", streamingLinks = new[] { "https://www.deezer.com/album/1" } });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var release = (await saved.Content.ReadFromJsonAsync<ReleaseResponse>(TestJson.Options))!;
        Assert.Equal(("Nuit Records", (string?)null), (release.Label, release.Upc));
        Assert.Equal(StreamingPlatform.Deezer, Assert.Single(release.StreamingLinks).Platform);

        var bad = await client.PutAsJsonAsync($"{url}/distribution", new { label = (string?)null, upc = (string?)null, streamingLinks = new[] { "deezer" } });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);

        // #26 : le message part traduit pour un client anglais
        using var english = new HttpRequestMessage(HttpMethod.Put, $"{url}/distribution")
        {
            Content = JsonContent.Create(new { label = (string?)null, upc = "123", streamingLinks = Array.Empty<string>() }),
        };
        english.Headers.AcceptLanguage.ParseAdd("en");
        var translated = await client.SendAsync(english);
        Assert.Contains("Invalid UPC/EAN code", await translated.Content.ReadAsStringAsync());
    }
}
