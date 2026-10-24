using System.Net;
using System.Net.Http.Json;
using SongVault.Api.Contracts.Bands;
using SongVault.Domain.Bands;
using SongVault.Api.Tests.Billing;

namespace SongVault.Api.Tests.Bands;

[Collection(ApiCollection.Name)]
public sealed class BandsEndpointsTests(SongVaultApiFactory factory)
{
    private static async Task<List<BandResponse>> MineAsync(HttpClient client)
        => (await client.GetFromJsonAsync<List<BandResponse>>("/api/bands", TestJson.Options))!;

    [Fact]
    public async Task Un_nouvel_inscrit_n_a_aucun_groupe_tant_qu_il_n_en_cree_pas()
    {
        var client = await factory.CreateNewUserClientAsync();

        Assert.Empty(await MineAsync(client));
        Assert.Empty(await MineAsync(client));                    // la lecture ne crée plus de groupe d'office

        var response = await client.PostAsJsonAsync("/api/bands", new { name = "Les Premiers" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);   // le premier groupe passe sur la formule gratuite
        var band = Assert.Single(await MineAsync(client));
        Assert.Equal("Les Premiers", band.Name);
        Assert.Equal(BandRole.Owner, band.Role);
    }

    [Fact]
    public async Task Post_cree_un_groupe_dont_je_suis_Owner()
    {
        var client = await factory.CreateNewUserClientAsync();
        var personal = await client.FirstBandIdAsync();           // premier groupe, gratuit
        await factory.GrantPlanAsync(personal);                   // #16 : un second groupe demande une formule payante

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
        var bandOfA = await a.FirstBandIdAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/bands/{bandOfA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await b.PutAsJsonAsync($"/api/bands/{bandOfA}", new { name = "Piraté" })).StatusCode);
    }
}