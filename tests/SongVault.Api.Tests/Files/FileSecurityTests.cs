using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;

using SongVault.Api.Contracts.Files;
using SongVault.Api.Contracts.Songs;

namespace SongVault.Api.Tests.Files;

[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public sealed class FileSecurityTests(SongVaultApiFactory factory)
{

    /// <summary>Crée un morceau et une version ; renvoie l'URL de la version.</summary>
    private static async Task<string> CreateVersionUrlAsync(HttpClient client)
    {
        var songs = await client.SongsUrlAsync();
        var songResponse = await client.PostAsJsonAsync(songs, new { title = "Sécurité" });
        var song = (await songResponse.Content.ReadFromJsonAsync<SongResponse>())!;
        var version = await client.PostAsJsonAsync($"{songs}/{song.Id}/versions", new { title = "v1", status = "Demo" });
        return version.Headers.Location!.ToString();
    }

    // ---------- 1. Signature binaire ----------
    [Fact]
    public async Task Exe_renomme_en_mp3_renvoie_400()
    {
        var client = factory.CreateUserClient();
        var url = await CreateVersionUrlAsync(client);
        byte[] exe = [0x4D, 0x5A, 0x90, 0x00, .. new byte[1020]];           // "MZ…" = exécutable Windows

        var response = await client.PostAsync($"{url}/files", TestFiles.Form(exe, "maquette.mp3"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("ne correspond pas", await response.Content.ReadAsStringAsync());
    }

    // ---------- 2. Nom de fichier piégé ----------
    [Theory]
    [InlineData("../../x.mp3")]
    [InlineData(@"..\..\x.mp3")]
    public async Task Nom_avec_chemin_est_nettoye(string maliciousName)
    {
        var client = factory.CreateUserClient();
        var url = await CreateVersionUrlAsync(client);

        var response = await client.PostAsync($"{url}/files", TestFiles.Form(TestFiles.Mp3(1024), maliciousName));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var file = await response.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options);
        Assert.Equal("x.mp3", file!.OriginalFileName);
    }

    // ---------- 3. Taille maximale ----------
    [Fact]
    public async Task Fichier_de_plus_de_50_Mo_renvoie_413()
    {
        var client = factory.CreateUserClient();
        var url = await CreateVersionUrlAsync(client);
        var tooBig = TestFiles.Mp3(50 * 1024 * 1024 + 1);                    // 1 octet de trop

        var response = await client.PostAsync($"{url}/files", TestFiles.Form(tooBig, "long.mp3"));

        if (response.StatusCode == HttpStatusCode.Created)
        {
            var created = await response.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options);
            Assert.Fail($"Fichier accepté : sizeBytes = {created!.SizeBytes}, limite = {50 * 1024 * 1024}");
        }
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    // ---------- 4. Limitation de débit ----------
    [Fact]
    public async Task Les_uploads_en_rafale_renvoient_429()
    {
        // Hôte dérivé : même base SQL Server, mais limite abaissée à 2 uploads par minute
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:UploadsPerMinute", "2"));
        var client = await limited.CreateNewUserClientAsync();
        var url = await CreateVersionUrlAsync(client);

        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 3; i++)
        {
            last = await client.PostAsync($"{url}/files", TestFiles.Form(TestFiles.Mp3(1024), $"prise-{i}.mp3"));
            statuses.Add(last.StatusCode);
        }

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Created, HttpStatusCode.TooManyRequests], statuses);
        Assert.True(last!.Headers.Contains("Retry-After"));
        Assert.Equal("application/problem+json", last.Content.Headers.ContentType?.MediaType);
    }

    // ---------- 5. En-têtes de sécurité ----------
    [Fact]
    public async Task Les_reponses_portent_les_en_tetes_de_securite()
    {
        var client = factory.CreateUserClient();
        var songs = await client.SongsUrlAsync();
        var response = await client.GetAsync(songs);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task La_CSP_stricte_ne_s_applique_qu_a_l_API()
    {
        var client = factory.CreateUserClient();
        var songs = await client.SongsUrlAsync();

        var api = await client.GetAsync(songs);
        var health = await client.GetAsync("/health/live");

        Assert.Contains("default-src 'none'", api.Headers.GetValues("Content-Security-Policy").Single());
        Assert.False(health.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("nosniff", health.Headers.GetValues("X-Content-Type-Options").Single());
    }
}