using System.Net;
using System.Net.Http.Json;

using SongVault.Api.Contracts.Bands;
using SongVault.Api.Contracts.Common;
using SongVault.Api.Contracts.Songs;

namespace SongVault.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public sealed class OwnershipTests(SongVaultApiFactory factory)
{
    private sealed record ResourcesOfA(Guid BandId, Guid SongId, string Song, string Version, string FileContent);

    /// <summary>L'utilisateur A crée, dans son groupe personnel, un morceau, une version et un fichier.</summary>
    private static async Task<ResourcesOfA> CreateResourcesAsync(HttpClient a)
    {
        var bandId = (await a.GetFromJsonAsync<List<BandResponse>>("/api/bands", TestJson.Options))![0].Id;
        var songs = $"/api/bands/{bandId}/songs";
        var song = (await (await a.PostAsJsonAsync(songs, new { title = "Secret de A" }))
            .Content.ReadFromJsonAsync<SongResponse>())!;
        var version = await a.PostAsJsonAsync($"{songs}/{song.Id}/versions", new { title = "v1", status = "Demo" });
        var versionUrl = version.Headers.Location!.ToString();
        var upload = await a.PostAsync($"{versionUrl}/files", TestFiles.Form(TestFiles.Mp3(1024), "a.mp3"));
        upload.EnsureSuccessStatusCode();
        return new ResourcesOfA(bandId, song.Id, $"{songs}/{song.Id}", versionUrl, upload.Headers.Location!.ToString());
    }

    // Méthode + cible : CHAQUE endpoint qui touche une ressource de A
    public static TheoryData<string, string> Endpoints => new()
    {
        { "GET",    "song" },
        { "PUT",    "song" },
        { "DELETE", "song" },
        { "GET",    "song/versions" },
        { "POST",   "song/versions" },
        { "GET",    "version" },
        { "PUT",    "version" },
        { "POST",   "version/files" },
        { "GET",    "file" },              // contenu du fichier : le plus souvent oublié
        { "DELETE", "fileItem" },
    };

    private static string UrlFor(ResourcesOfA r, string target) => target switch
    {
        "song" => r.Song,
        "song/versions" => $"{r.Song}/versions",
        "version" => r.Version,
        "version/files" => $"{r.Version}/files",
        "file" => r.FileContent,
        "fileItem" => r.FileContent[..r.FileContent.LastIndexOf("/content", StringComparison.Ordinal)],
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };

    private static HttpContent? BodyFor(string method, string target) => (method, target) switch
    {
        ("POST", "version/files") => TestFiles.Form(TestFiles.Mp3(1024), "b.mp3"),
        ("PUT", _) or ("POST", _) => JsonContent.Create(new { title = "Piraté", status = "Final" }),
        _ => null,
    };

    /// <summary>Rien n'a bougé chez A.</summary>
    private static async Task AssertUntouchedAsync(HttpClient a, ResourcesOfA r)
    {
        var song = await a.GetFromJsonAsync<SongResponse>(r.Song);
        Assert.Equal("Secret de A", song!.Title);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync(r.FileContent)).StatusCode);
    }

    [Theory, MemberData(nameof(Endpoints))]
    public async Task Non_membre_recoit_404_sur_les_ressources_du_groupe(string method, string target)
    {
        var a = await factory.CreateNewUserClientAsync();
        var b = await factory.CreateNewUserClientAsync();
        var r = await CreateResourcesAsync(a);

        var response = await b.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), UrlFor(r, target)) { Content = BodyFor(method, target) });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertUntouchedAsync(a, r);
    }

    [Theory, MemberData(nameof(Endpoints))]
    public async Task Membre_d_un_autre_groupe_recoit_404_meme_avec_son_propre_bandId(string method, string target)
    {
        var a = await factory.CreateNewUserClientAsync();
        var b = await factory.CreateNewUserClientAsync();
        var r = await CreateResourcesAsync(a);
        var bandOfB = (await b.GetFromJsonAsync<List<BandResponse>>("/api/bands", TestJson.Options))![0].Id;

        // B est bien membre du groupe de l'URL, mais le morceau appartient au groupe de A
        var url = UrlFor(r, target).Replace($"/bands/{r.BandId}/", $"/bands/{bandOfB}/");
        var response = await b.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), url) { Content = BodyFor(method, target) });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertUntouchedAsync(a, r);
    }

    [Fact]
    public async Task La_liste_de_B_ne_contient_pas_les_morceaux_de_A()
    {
        var a = await factory.CreateNewUserClientAsync();
        var b = await factory.CreateNewUserClientAsync();
        var r = await CreateResourcesAsync(a);

        var list = await b.GetFromJsonAsync<PagedResponse<SongResponse>>($"{await b.SongsUrlAsync()}?pageSize=50");

        Assert.DoesNotContain(list!.Items, s => s.Id == r.SongId);
    }

    [Fact]
    public async Task Membre_de_deux_groupes_voit_chaque_liste_separement()
    {
        var a = await factory.CreateNewUserClientAsync();
        var personal = await a.SongsUrlAsync();
        var second = (await (await a.PostAsJsonAsync("/api/bands", new { name = "Les Autres" }))
            .Content.ReadFromJsonAsync<BandResponse>(TestJson.Options))!;
        await a.PostAsJsonAsync(personal, new { title = "Perso" });
        await a.PostAsJsonAsync($"/api/bands/{second.Id}/songs", new { title = "Groupe" });

        var p = await a.GetFromJsonAsync<PagedResponse<SongResponse>>(personal);
        var g = await a.GetFromJsonAsync<PagedResponse<SongResponse>>($"/api/bands/{second.Id}/songs");

        Assert.Equal(["Perso"], p!.Items.Select(s => s.Title));
        Assert.Equal(["Groupe"], g!.Items.Select(s => s.Title));
    }
}