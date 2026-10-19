using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;

using SongVault.Api.Contracts.ServiceStatus;

namespace SongVault.Api.Tests.ServiceStatus;

/// <summary>#18 : GET /api/status, ouvert à tous, pour la page /statut et la vérification de disponibilité.</summary>
[Collection(ApiCollection.Name)]
public sealed class StatusEndpointTests(SongVaultApiFactory factory)
{
    [Fact]
    public async Task Le_statut_se_lit_sans_compte_et_n_est_jamais_mis_en_cache()
    {
        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);       // aucune tâche en retard laissée par d'autres tests
        using var visitor = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var response = await visitor.GetAsync("/api/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var status = (await response.Content.ReadFromJsonAsync<StatusResponse>(TestJson.Options))!;
        Assert.Equal("ok", status.Status);
        string[] names = ["Application", "Base de données", "Tâches de fond (e-mails, analyses, aperçus)"];
        Assert.Equal(names, status.Components.Select(c => c.Name));
    }
}