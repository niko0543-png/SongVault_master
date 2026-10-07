using System.Net;
using System.Net.Http.Json;

using SongVault.Api.Contracts.Common;
using SongVault.Api.Contracts.Songs;

namespace SongVault.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public sealed class OwnershipTests(SongVaultApiFactory factory)
{
    private sealed record ResourcesOfA(Guid SongId, string Song, string Version, string FileContent);

    /// <summary>L'utilisateur A crée un morceau, une version et un fichier.</summary>
    private static async Task<ResourcesOfA> CreateResourcesAsync(HttpClient a)
    {
        var song = (await (await a.PostAsJsonAsync("/api/songs", new { title = "Secret de A" }))
            .Content.ReadFromJsonAsync<SongResponse>())!;
        var version = await a.PostAsJsonAsync($"/api/songs/{song.Id}/versions", new { title = "v1", status = "Demo" });
        var versionUrl = version.Headers.Location!.ToString();
        var upload = await a.PostAsync($"{versionUrl}/files", TestFiles.Form(TestFiles.Mp3(1024), "a.mp3"));
        upload.EnsureSuccessStatusCode();
        return new ResourcesOfA(song.Id, $"/api/songs/{song.Id}", versionUrl, upload.Headers.Location!.ToString());
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

    [Theory, MemberData(nameof(Endpoints))]
    public async Task B_recoit_404_sur_les_ressources_de_A(string method, string target)
    {
        var a = await factory.CreateNewUserClientAsync();
        var b = await factory.CreateNewUserClientAsync();
        var r = await CreateResourcesAsync(a);

        var url = target switch
        {
            "song" => r.Song,
            "song/versions" => $"{r.Song}/versions",
            "version" => r.Version,
            "version/files" => $"{r.Version}/files",
            "file" => r.FileContent,
            "fileItem" => r.FileContent[..r.FileContent.LastIndexOf("/content", StringComparison.Ordinal)],
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
        HttpContent? body = (method, target) switch
        {
            ("POST", "version/files") => TestFiles.Form(TestFiles.Mp3(1024), "b.mp3"),
            ("PUT", _) or ("POST", _) => JsonContent.Create(new { title = "Piraté", status = "Final" }),
            _ => null,
        };

        var response = await b.SendAsync(new HttpRequestMessage(new HttpMethod(method), url) { Content = body });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Et rien n'a bougé chez A
        var song = await a.GetFromJsonAsync<SongResponse>(r.Song);
        Assert.Equal("Secret de A", song!.Title);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync(r.FileContent)).StatusCode);
    }

    [Fact]
    public async Task La_liste_de_B_ne_contient_pas_les_morceaux_de_A()
    {
        var a = await factory.CreateNewUserClientAsync();
        var b = await factory.CreateNewUserClientAsync();
        var r = await CreateResourcesAsync(a);

        var list = await b.GetFromJsonAsync<PagedResponse<SongResponse>>("/api/songs?pageSize=50");

        Assert.DoesNotContain(list!.Items, s => s.Id == r.SongId);
    }
}