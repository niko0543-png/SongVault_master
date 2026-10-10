using System.Net;
using System.Net.Http.Json;

using SongVault.Api.Contracts.Bands;
using SongVault.Api.Contracts.Files;
using SongVault.Domain.Bands;

namespace SongVault.Api.Tests.Bands;

[Collection(ApiCollection.Name)]
public sealed class BandRolesTests(SongVaultApiFactory factory)
{
    /// <summary>URL d'un groupe rempli par son Owner : morceau, version, fichier (contenu et ressource).</summary>
    private sealed record Urls(Guid BandId, string Songs, string Song, string Version, string FileContent, string File);

    /// <summary>Un Owner remplit son groupe, puis un nouvel utilisateur y est ajouté avec le rôle demandé.</summary>
    private async Task<(Urls Urls, HttpClient Member)> ArrangeAsync(BandRole role)
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var songs = $"/api/bands/{bandId}/songs";

        var song = (await owner.PostAsJsonAsync(songs, new { title = "Rôles" })).Headers.Location!.ToString();
        var version = (await owner.PostAsJsonAsync($"{song}/versions", new { title = "v1", status = "Demo" }))
            .Headers.Location!.ToString();
        var upload = await owner.PostAsync($"{version}/files", TestFiles.Form(TestFiles.Mp3(1024), "a.mp3"));
        var file = (await upload.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!;

        var member = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, member, role);

        var urls = new Urls(bandId, songs, song, version, upload.Headers.Location!.ToString(), $"{version}/files/{file.Id}");
        return (urls, member);
    }

    /// <summary>Les 7 écritures sur le contenu du groupe, dans un ordre où chacune peut réussir (morceau supprimé en dernier).</summary>
    private static (string Name, Func<HttpClient, Task<HttpResponseMessage>> Send)[] Writes(Urls u) =>
    [
        ("POST morceau", c => c.PostAsJsonAsync(u.Songs, new { title = "Écriture" })),
        ("PUT morceau", c => c.PutAsJsonAsync(u.Song, new { title = "Écriture" })),
        ("POST version", c => c.PostAsJsonAsync($"{u.Song}/versions", new { title = "v2", status = "Demo" })),
        ("PUT version", c => c.PutAsJsonAsync(u.Version, new { title = "v1 bis", status = "Studio" })),
        ("POST fichier", c => c.PostAsync($"{u.Version}/files", TestFiles.Form(TestFiles.Mp3(1024), "b.mp3"))),
        ("DELETE fichier", c => c.DeleteAsync(u.File)),
        ("DELETE morceau", c => c.DeleteAsync(u.Song)),
    ];

    private static Task<HttpResponseMessage> RenameAsync(HttpClient client, Guid bandId)
        => client.PutAsJsonAsync($"/api/bands/{bandId}", new { name = "Renommé" });

    [Fact]
    public async Task Guest_recoit_403_sur_chaque_ecriture()
    {
        var (urls, guest) = await ArrangeAsync(BandRole.Guest);

        foreach (var (name, send) in Writes(urls))
        {
            var response = await send(guest);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{name} : {(int)response.StatusCode}");
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await RenameAsync(guest, urls.BandId)).StatusCode);
    }

    [Fact]
    public async Task Guest_recoit_403_meme_avec_un_corps_invalide()
    {
        var (urls, guest) = await ArrangeAsync(BandRole.Guest);

        var response = await guest.PostAsJsonAsync(urls.Songs, new { });     // sans titre : 400 pour un Member

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Guest_lit_morceaux_versions_et_fichiers()
    {
        var (urls, guest) = await ArrangeAsync(BandRole.Guest);

        foreach (var url in new[] { urls.Songs, urls.Song, $"{urls.Song}/versions", urls.Version, urls.FileContent, $"/api/bands/{urls.BandId}" })
            Assert.True((await guest.GetAsync(url)).IsSuccessStatusCode, url);
    }

    [Fact]
    public async Task Guest_voit_son_role_dans_ses_groupes()
    {
        var (urls, guest) = await ArrangeAsync(BandRole.Guest);

        var band = Assert.Single((await guest.GetFromJsonAsync<List<BandResponse>>("/api/bands", TestJson.Options))!);

        Assert.Equal(urls.BandId, band.Id);
        Assert.Equal(BandRole.Guest, band.Role);
    }

    [Fact]
    public async Task Member_ecrit_mais_ne_renomme_pas_le_groupe()
    {
        var (urls, member) = await ArrangeAsync(BandRole.Member);

        foreach (var (name, send) in Writes(urls))
        {
            var response = await send(member);
            Assert.True(response.IsSuccessStatusCode, $"{name} : {(int)response.StatusCode}");
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await RenameAsync(member, urls.BandId)).StatusCode);
    }

    [Fact]
    public async Task Owner_renomme_le_groupe()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await RenameAsync(owner, bandId)).StatusCode);
    }
}