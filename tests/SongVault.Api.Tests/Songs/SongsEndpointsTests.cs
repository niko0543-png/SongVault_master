using System.Net;
using System.Net.Http.Json;

using SongVault.Api.Contracts.Songs;

namespace SongVault.Api.Tests.Songs;

[Collection(ApiCollection.Name)]
public sealed class SongsEndpointsTests(SongVaultApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateUserClient();

    [Fact]
    public async Task Post_valide_renvoie_201_avec_Location()
    {
        var songs = await _client.SongsUrlAsync();
        var response = await _client.PostAsJsonAsync(songs, new { title = "Nocturne", artist = "Band" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var song = await response.Content.ReadFromJsonAsync<SongResponse>();
        Assert.NotNull(song);
        Assert.EndsWith($"{songs}/{song.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Post_sans_titre_renvoie_400_ProblemDetails()
    {
        var songs = await _client.SongsUrlAsync();
        var response = await _client.PostAsJsonAsync(songs, new { artist = "Sans titre" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"Title\"", body);
    }

    [Fact]
    public async Task Get_id_inconnu_renvoie_404_ProblemDetails()
    {
        var songs = await _client.SongsUrlAsync();
        var response = await _client.GetAsync($"{songs}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}