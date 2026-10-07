using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;

using SongVault.Api.Contracts.Songs;
using SongVault.Api.Contracts.Versions;

namespace SongVault.Api.Tests.Files;

[Collection(ApiCollection.Name)]
public sealed class SongFilesEndpointsTests(SongVaultApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> CreateVersionUrlAsync()
    {
        var song = (await (await _client.PostAsJsonAsync("/api/songs", new { title = "Fichiers" }))
            .Content.ReadFromJsonAsync<SongResponse>())!;
        var versionResponse = await _client.PostAsJsonAsync($"/api/songs/{song.Id}/versions", new { title = "v1", status = "Demo" });
        return versionResponse.Headers.Location!.ToString();
    }

    [Fact]
    public async Task Upload_puis_telechargement_renvoie_exactement_les_memes_octets()
    {
        var versionUrl = await CreateVersionUrlAsync();
        var bytes = TestFiles.Mp3(64 * 1024);

        var upload = await _client.PostAsync($"{versionUrl}/files", TestFiles.Form(bytes, "maquette.mp3"));

        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var downloaded = await _client.GetByteArrayAsync(upload.Headers.Location);
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(downloaded));
    }

    [Fact]
    public async Task Requete_Range_renvoie_206()
    {
        var versionUrl = await CreateVersionUrlAsync();
        var upload = await _client.PostAsync($"{versionUrl}/files", TestFiles.Form(TestFiles.Mp3(4096), "a.mp3"));

        var request = new HttpRequestMessage(HttpMethod.Get, upload.Headers.Location);
        request.Headers.Range = new RangeHeaderValue(0, 99);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(100, (await response.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task Upload_exe_renvoie_400()
    {
        var versionUrl = await CreateVersionUrlAsync();

        var response = await _client.PostAsync($"{versionUrl}/files", TestFiles.Form([1, 2, 3], "virus.exe"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}