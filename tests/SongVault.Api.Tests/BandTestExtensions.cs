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

    /// <summary>Premier groupe du client : son groupe personnel, créé au premier appel s'il n'est membre de rien.</summary>
    public static async Task<Guid> FirstBandIdAsync(this HttpClient client)
        => (await client.GetFromJsonAsync<List<BandResponse>>("/api/bands", TestJson.Options))![0].Id;

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