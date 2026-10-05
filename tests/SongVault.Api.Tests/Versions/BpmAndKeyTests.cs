using System.Net;
using System.Net.Http.Json;

using SongVault.Api.Contracts.Songs;

namespace SongVault.Api.Tests.Versions;

[Collection(ApiCollection.Name)]
public sealed class BpmAndKeyTests(SongVaultApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> CreateSongAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/songs", new { title = "Tonalités" });
        return (await response.Content.ReadFromJsonAsync<SongResponse>())!.Id;
    }

    [Fact]
    public async Task Tonalite_invalide_renvoie_422()
    {
        var songId = await CreateSongAsync();

        var response = await _client.PostAsJsonAsync($"/api/songs/{songId}/versions",
            new { title = "v1", status = "Demo", key = "H" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Bpm_hors_limites_renvoie_400()
    {
        var songId = await CreateSongAsync();

        var response = await _client.PostAsJsonAsync($"/api/songs/{songId}/versions",
            new { title = "v1", status = "Demo", bpm = 500 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}