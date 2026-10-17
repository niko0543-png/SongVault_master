using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using SongVault.Api.Contracts.Files;
using SongVault.Api.Contracts.Sharing;
using SongVault.Api.Contracts.Songs;
using SongVault.Api.Contracts.Versions;
using SongVault.Domain.Bands;
using SongVault.Domain.Sharing;

namespace SongVault.Api.Tests.Sharing;

[Collection(ApiCollection.Name)]
public sealed class ShareLinksEndpointsTests(SongVaultApiFactory factory)
{
    private sealed record SharedVersion(Guid BandId, string SongUrl, Guid SongId, Guid VersionId, Guid FileId, byte[] Bytes);

    /// <summary>Morceau, version et fichier MP3 dans le premier groupe du client ; scanned : analyse antivirus faite (#17).</summary>
    private async Task<SharedVersion> VersionWithAudioAsync(HttpClient client, string title = "Aube", bool scanned = true)
    {
        var bandId = await client.FirstBandIdAsync();
        var created = await client.PostAsJsonAsync($"/api/bands/{bandId}/songs", new { title });
        var song = (await created.Content.ReadFromJsonAsync<SongResponse>(TestJson.Options))!;
        var songUrl = created.Headers.Location!.ToString();
        var version = await client.PostAsJsonAsync($"{songUrl}/versions", new { title = "Mix 2", status = "Studio" });
        var v = (await version.Content.ReadFromJsonAsync<SongVersionResponse>(TestJson.Options))!;
        var bytes = TestFiles.Mp3(8192);
        var upload = await client.PostAsync($"{version.Headers.Location}/files", TestFiles.Form(bytes, $"{title}-mix.mp3"));
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var file = (await upload.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!;
        if (scanned) await SongVaultApiFactory.RunDueJobsAsync(factory.Services);
        return new SharedVersion(bandId, songUrl, song.Id, v.Id, file.Id, bytes);
    }

    private static async Task<ShareLinkCreatedResponse> ShareAsync(
        HttpClient client, Guid bandId, ShareTargetType type, Guid targetId, string? password = null, int? days = null)
    {
        var response = await client.PostAsJsonAsync($"/api/bands/{bandId}/share-links",
            new { targetType = type.ToString(), targetId, label = "Studio Delta", expiresInDays = days, password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ShareLinkCreatedResponse>(TestJson.Options))!;
    }

    /// <summary>https://…/s/{jeton} → /api/share/{jeton}</summary>
    private static string Api(ShareLinkCreatedResponse created) => $"/api/share/{created.Url[(created.Url.LastIndexOf('/') + 1)..]}";

    /// <summary>Navigateur sans compte : garde ses cookies, comme un vrai.</summary>
    private HttpClient Visitor() => factory.CreateClient();

    private static async Task<List<ShareLinkAccessResponse>> AccessesAsync(HttpClient client, Guid bandId, Guid linkId)
        => (await client.GetFromJsonAsync<List<ShareLinkAccessResponse>>(
            $"/api/bands/{bandId}/share-links/{linkId}/accesses", TestJson.Options))!;

    [Fact]
    [Trait("Category", "Security")]
    public async Task Le_visiteur_ne_voit_un_fichier_qu_une_fois_declare_sain()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var shared = await VersionWithAudioAsync(owner, scanned: false);
        var link = await ShareAsync(owner, shared.BandId, ShareTargetType.Version, shared.VersionId);
        var visitor = Visitor();
        var audio = $"{Api(link)}/files/{shared.FileId}/content";

        var before = await visitor.GetFromJsonAsync<SharedContentResponse>(Api(link), TestJson.Options);
        Assert.Empty(before!.Tracks.Single().Files);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(audio)).StatusCode);

        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);

        var after = await visitor.GetFromJsonAsync<SharedContentResponse>(Api(link), TestJson.Options);
        Assert.Equal(shared.FileId, Assert.Single(after!.Tracks.Single().Files).Id);
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync(audio)).StatusCode);
    }

    [Fact]
    public async Task Sans_compte_on_ecoute_la_version_et_rien_d_autre()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var shared = await VersionWithAudioAsync(owner);
        var link = await ShareAsync(owner, shared.BandId, ShareTargetType.Version, shared.VersionId);
        var visitor = Visitor();

        Assert.StartsWith("http://localhost:8080/s/", link.Url);
        var page = await visitor.GetAsync(Api(link));
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("noindex", page.Headers.GetValues("X-Robots-Tag").Single());
        var content = (await page.Content.ReadFromJsonAsync<SharedContentResponse>(TestJson.Options))!;
        Assert.Equal("Aube", content.Title);
        var track = Assert.Single(content.Tracks);
        Assert.Equal((1, "Mix 2"), (track.VersionNumber, track.VersionTitle));
        Assert.Equal(shared.FileId, Assert.Single(track.Files).Id);

        var audio = await visitor.GetAsync($"{Api(link)}/files/{shared.FileId}/content");
        Assert.Equal(HttpStatusCode.OK, audio.StatusCode);
        Assert.Equal(shared.Bytes, await audio.Content.ReadAsByteArrayAsync());
        Assert.Null(audio.Content.Headers.ContentDisposition);                // lecture, pas téléchargement

        // Aucun accès en écriture : les routes du groupe exigent toujours un compte
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync(shared.SongUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await visitor.PutAsJsonAsync(shared.SongUrl, new { title = "Piraté" })).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await visitor.PostAsJsonAsync(Api(link), new { })).StatusCode);
    }

    [Fact]
    public async Task Changer_l_identifiant_du_fichier_ne_mene_a_aucun_autre_fichier()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var shared = await VersionWithAudioAsync(owner, "Aube");
        var other = await VersionWithAudioAsync(owner, "Brume");                              // même groupe
        var stranger = await VersionWithAudioAsync(await factory.CreateNewUserClientAsync(), "Ailleurs");
        var lyrics = await owner.PostAsync($"{shared.SongUrl}/versions/{shared.VersionId}/files",
            TestFiles.Form(Encoding.UTF8.GetBytes("Paroles du refrain"), "paroles.txt"));
        var lyricsId = (await lyrics.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!.Id;
        var link = await ShareAsync(owner, shared.BandId, ShareTargetType.Version, shared.VersionId);
        var visitor = Visitor();

        foreach (var fileId in new[] { other.FileId, stranger.FileId, lyricsId, Guid.CreateVersion7() })
            Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"{Api(link)}/files/{fileId}/content")).StatusCode);

        var content = await visitor.GetFromJsonAsync<SharedContentResponse>(Api(link), TestJson.Options);
        Assert.Equal([shared.FileId], content!.Tracks.Single().Files.Select(f => f.Id));   // ni le .txt ni l'autre morceau
    }

    [Fact]
    public async Task Un_lien_revoque_ne_marche_plus_et_reste_dans_la_liste()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var shared = await VersionWithAudioAsync(owner);
        var link = await ShareAsync(owner, shared.BandId, ShareTargetType.Version, shared.VersionId);
        var visitor = Visitor();
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync(Api(link))).StatusCode);

        var revoke = await owner.PostAsync($"/api/bands/{shared.BandId}/share-links/{link.Link.Id}/revoke", null);

        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(Api(link))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await visitor.GetAsync($"{Api(link)}/files/{shared.FileId}/content")).StatusCode);
        var list = await owner.GetFromJsonAsync<List<ShareLinkResponse>>(
            $"/api/bands/{shared.BandId}/share-links?versionId={shared.VersionId}", TestJson.Options);
        var listed = Assert.Single(list!);
        Assert.Equal((ShareLinkStatus.Revoked, 1), (listed.Status, listed.OpenCount));
    }

    [Fact]
    public async Task Un_lien_expire_ne_marche_plus()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var host = factory.WithClock(clock);
        var owner = await host.CreateNewUserClientAsync();
        var shared = await VersionWithAudioAsync(owner);
        var link = await ShareAsync(owner, shared.BandId, ShareTargetType.Version, shared.VersionId, days: 1);
        var visitor = host.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync(Api(link))).StatusCode);

        clock.Advance(TimeSpan.FromDays(1));

        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(Api(link))).StatusCode);
    }

    [Fact]
    public async Task Chaque_acces_est_journalise_sans_l_adresse_ip()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var shared = await VersionWithAudioAsync(owner);
        var link = await ShareAsync(owner, shared.BandId, ShareTargetType.Version, shared.VersionId);
        var visitor = Visitor();
        visitor.DefaultRequestHeaders.UserAgent.ParseAdd("TestBrowser/1.0");
        // TestServer ne donne pas d'IP : celle de nginx, comme en production (ForwardedHeaders)
        visitor.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.7");
        var file = $"{Api(link)}/files/{shared.FileId}/content";

        await visitor.GetAsync(Api(link));
        await visitor.GetAsync(file);
        var resume = new HttpRequestMessage(HttpMethod.Get, file) { Headers = { Range = new RangeHeaderValue(4096, null) } };
        Assert.Equal(HttpStatusCode.PartialContent, (await visitor.SendAsync(resume)).StatusCode);

        var accesses = await AccessesAsync(owner, shared.BandId, link.Link.Id);
        Assert.Equal([ShareAccessKind.Played, ShareAccessKind.Opened], accesses.Select(a => a.Kind));   // la reprise ne compte pas
        Assert.Equal("Aube-mix.mp3", accesses[0].FileName);
        Assert.All(accesses, a => Assert.Equal("TestBrowser/1.0", a.UserAgent));
        Assert.All(accesses, a => Assert.Equal(8, a.Visitor?.Length));       // une empreinte, jamais l'IP
        Assert.Single(accesses.Select(a => a.Visitor).Distinct());
    }

    [Fact]
    public async Task Un_lien_protege_demande_le_mot_de_passe_une_fois_par_navigateur()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var shared = await VersionWithAudioAsync(owner);
        var link = await ShareAsync(owner, shared.BandId, ShareTargetType.Version, shared.VersionId, password: "écoute-v3");
        var visitor = Visitor();
        var file = $"{Api(link)}/files/{shared.FileId}/content";

        Assert.Equal(HttpStatusCode.Forbidden, (await visitor.GetAsync(Api(link))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await visitor.GetAsync(file)).StatusCode);
        var wrong = await visitor.PostAsJsonAsync($"{Api(link)}/unlock", new { password = "mauvais" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrong.StatusCode);

        var unlock = await visitor.PostAsJsonAsync($"{Api(link)}/unlock", new { password = "écoute-v3" });

        Assert.Equal(HttpStatusCode.NoContent, unlock.StatusCode);
        var cookie = unlock.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains($"path={Api(link)}", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync(Api(link))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync(file)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Visitor().GetAsync(Api(link))).StatusCode);   // autre navigateur
        var accesses = await AccessesAsync(owner, shared.BandId, link.Link.Id);
        Assert.Contains(accesses, a => a.Kind == ShareAccessKind.WrongPassword);
    }

    [Fact]
    public async Task Une_sortie_partagee_donne_ses_pistes_dans_l_ordre()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var aube = await VersionWithAudioAsync(owner, "Aube");
        var brume = await VersionWithAudioAsync(owner, "Brume");
        var release = await owner.PostAsJsonAsync($"/api/bands/{aube.BandId}/releases", new { title = "Nuits blanches", type = "EP" });
        var releaseUrl = release.Headers.Location!.ToString();
        var releaseId = Guid.Parse(releaseUrl[(releaseUrl.LastIndexOf('/') + 1)..]);
        await owner.PutAsJsonAsync($"{releaseUrl}/tracks", new
        {
            tracks = new[] { new { songId = brume.SongId, versionId = brume.VersionId }, new { songId = aube.SongId, versionId = aube.VersionId } },
        });
        var link = await ShareAsync(owner, aube.BandId, ShareTargetType.Release, releaseId);
        var visitor = Visitor();

        var content = await visitor.GetFromJsonAsync<SharedContentResponse>(Api(link), TestJson.Options);

        Assert.Equal("Nuits blanches", content!.Title);
        Assert.Equal(["Brume", "Aube"], content.Tracks.Select(t => t.SongTitle));
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync($"{Api(link)}/files/{aube.FileId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"{Api(link)}/cover")).StatusCode);   // pas de pochette
    }

    [Fact]
    public async Task Supprimer_le_morceau_efface_ses_liens()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var shared = await VersionWithAudioAsync(owner);
        var link = await ShareAsync(owner, shared.BandId, ShareTargetType.Version, shared.VersionId);
        var visitor = Visitor();
        await visitor.GetAsync(Api(link));                                    // une ligne au journal

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(shared.SongUrl)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(Api(link))).StatusCode);
    }

    [Fact]
    public async Task Un_Guest_ne_voit_ni_ne_cree_de_liens_et_un_autre_groupe_n_existe_pas()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var shared = await VersionWithAudioAsync(owner);
        var guest = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(shared.BandId, guest, BandRole.Guest);
        var outsider = await factory.CreateNewUserClientAsync();
        var list = $"/api/bands/{shared.BandId}/share-links?versionId={shared.VersionId}";
        var body = new { targetType = "Version", targetId = shared.VersionId };

        Assert.Equal(HttpStatusCode.Forbidden, (await guest.GetAsync(list)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await guest.PostAsJsonAsync($"/api/bands/{shared.BandId}/share-links", body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(list)).StatusCode);
        // Version d'un autre groupe, depuis son propre groupe : introuvable
        var outsiderBand = await outsider.FirstBandIdAsync();
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.PostAsJsonAsync($"/api/bands/{outsiderBand}/share-links", body)).StatusCode);
    }
}