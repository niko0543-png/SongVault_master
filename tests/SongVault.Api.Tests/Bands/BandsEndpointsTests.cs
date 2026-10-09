using System.Net;
using System.Net.Http.Json;

using SongVault.Api.Contracts.Bands;
using SongVault.Domain.Bands;

namespace SongVault.Api.Tests.Bands;

[Collection(ApiCollection.Name)]
public sealed class BandsEndpointsTests(SongVaultApiFactory factory)
{
    private static async Task<List<BandResponse>> MineAsync(HttpClient client)
        => (await client.GetFromJsonAsync<List<BandResponse>>("/api/bands", TestJson.Options))!;

    [Fact]
    public async Task Premier_appel_cree_un_seul_groupe_personnel()
    {
        var client = await factory.CreateNewUserClientAsync();

        var first = Assert.Single(await MineAsync(client));
        var second = Assert.Single(await MineAsync(client));

        Assert.StartsWith("Groupe de user-", first.Name);
        Assert.Equal(BandRole.Owner, first.Role);
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task Post_cree_un_groupe_dont_je_suis_Owner()
    {
        var client = await factory.CreateNewUserClientAsync();
        await MineAsync(client);                                  // crée le groupe personnel

        var response = await client.PostAsJsonAsync("/api/bands", new { name = "Les Autres" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var band = (await response.Content.ReadFromJsonAsync<BandResponse>(TestJson.Options))!;
        Assert.EndsWith($"/api/bands/{band.Id}", response.Headers.Location!.ToString());
        Assert.Equal(2, (await MineAsync(client)).Count);
    }

    [Fact]
    public async Task Non_membre_recoit_404_sur_le_groupe()
    {
        var a = await factory.CreateNewUserClientAsync();
        var b = await factory.CreateNewUserClientAsync();
        var bandOfA = (await MineAsync(a))[0].Id;

        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/bands/{bandOfA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await b.PutAsJsonAsync($"/api/bands/{bandOfA}", new { name = "Piraté" })).StatusCode);
    }
}