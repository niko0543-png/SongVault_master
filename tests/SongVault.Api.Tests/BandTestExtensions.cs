using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Api.Contracts.Auth;
using SongVault.Api.Contracts.Bands;
using SongVault.Domain.Bands;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests;

public static class BandTestExtensions
{
    /// <summary>Identifiant Identity de l'utilisateur du client (GET /api/auth/me).</summary>
    public static async Task<string> UserIdAsync(this HttpClient client)
        => (await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", TestJson.Options))!.Id;

    /// <summary>
    /// Premier groupe du client. Un nouvel inscrit n'en a aucun (il le crée depuis la page d'accueil) :
    /// le test le crée alors, comme le ferait l'utilisateur.
    /// </summary>
    public static async Task<Guid> FirstBandIdAsync(this HttpClient client)
    {
        var bands = (await client.GetFromJsonAsync<List<BandResponse>>("/api/bands", TestJson.Options))!;
        if (bands.Count > 0) return bands[0].Id;

        var created = await client.PostAsJsonAsync("/api/bands", new { name = "Groupe de test" });
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<BandResponse>(TestJson.Options))!.Id;
    }

    /// <summary>
    /// Ajoute directement en base l'utilisateur du client au groupe, avec ce rôle.
    /// Raccourci de test en attendant les invitations (#3).
    /// </summary>
    public static async Task AddMemberAsync(this SongVaultApiFactory factory, Guid bandId, HttpClient member, BandRole role)
    {
        var userId = await member.UserIdAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
        var band = await db.Bands.Include(b => b.Memberships).SingleAsync(b => b.Id == bandId);
        band.AddMember(userId, role, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }
}