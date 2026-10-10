using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;

using SongVault.Api.Contracts.Bands;

namespace SongVault.Api.Tests;

public static class AuthTestExtensions
{
    /// <summary>
    /// Client connecté avec un NOUVEL utilisateur (adresse aléatoire, ou celle donnée).
    /// Fonctionne aussi sur un hôte dérivé (WithWebHostBuilder), qui ne connaît pas le cookie par défaut.
    /// </summary>
    public static async Task<HttpClient> CreateNewUserClientAsync(
        this WebApplicationFactory<Program> factory, string? email = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var cookie = await SongVaultApiFactory.RegisterAndLoginAsync(client, email ?? $"user-{Guid.NewGuid():N}@test.local");
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    /// <summary>Route des morceaux du groupe personnel de l'utilisateur du client (créé au premier GET /api/bands).</summary>
    public static async Task<string> SongsUrlAsync(this HttpClient client)
    {
        var bands = await client.GetFromJsonAsync<List<BandResponse>>("/api/bands", TestJson.Options);
        return $"/api/bands/{bands![0].Id}/songs";
    }
}