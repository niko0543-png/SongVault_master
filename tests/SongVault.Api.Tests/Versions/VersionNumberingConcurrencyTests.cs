using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using SongVault.Api.Contracts.Songs;
using SongVault.Api.Contracts.Versions;

namespace SongVault.Api.Tests.Versions;

[Collection(ApiCollection.Name)]
public sealed class VersionNumberingConcurrencyTests(SongVaultApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateUserClient();

    [Fact]
    public async Task Dix_creations_paralleles_donnent_des_numeros_distincts_sans_erreur_500()
    {
        var songResponse = await _client.PostAsJsonAsync("/api/songs", new { title = "Concurrence" });
        var song = (await songResponse.Content.ReadFromJsonAsync<SongResponse>())!;

        var responses = await Task.WhenAll(Enumerable.Range(1, 10).Select(i =>
            _client.PostAsJsonAsync($"/api/songs/{song.Id}/versions", new { title = $"Prise {i}", status = "Demo" })));

        // Seuls 201 (créé) ou 409 (tentatives épuisées) sont acceptables
        Assert.All(responses, r => Assert.True(
            r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            $"Statut inattendu : {(int)r.StatusCode}"));

        var created = responses.Where(r => r.StatusCode == HttpStatusCode.Created).ToList();
        Assert.NotEmpty(created);

        var numbers = new List<int>();
        foreach (var r in created)
            numbers.Add((await r.Content.ReadFromJsonAsync<SongVersionResponse>(Json))!.Number);

        numbers.Sort();
        Assert.Equal(Enumerable.Range(1, numbers.Count), numbers);   // 1..n, sans doublon ni trou
    }
}