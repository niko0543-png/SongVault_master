using System.Net.Http.Json;

using SongVault.Api.Contracts.Common;
using SongVault.Api.Contracts.Songs;

namespace SongVault.Api.Tests.Songs;

[Collection(ApiCollection.Name)]
public sealed class SongSearchTests(SongVaultApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateUserClient();

    [Fact]
    public async Task Search_filtre_par_titre_sans_tenir_compte_de_la_casse()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];          // base partagée : titres uniques
        var songs = await _client.SongsUrlAsync();
        await _client.PostAsJsonAsync(songs, new { title = $"Goodbye Blue Sky {marker}" });
        await _client.PostAsJsonAsync(songs, new { title = $"Hello {marker}" });

        var result = await _client.GetFromJsonAsync<PagedResponse<SongResponse>>($"{songs}?search=GOODBYE%20BLUE%20SKY%20{marker}");

        var song = Assert.Single(result!.Items);
        Assert.StartsWith("Goodbye", song.Title);
    }
}