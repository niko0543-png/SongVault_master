using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Api.Contracts.Bands;
using SongVault.Application.Abstractions;
using SongVault.Domain.Bands;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests.Accounts;

[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public sealed class AccountDeletionTests(SongVaultApiFactory factory)
{
    private static string NewEmail() => $"delete-{Guid.NewGuid():N}@test.local";

    private static HttpClient Anonymous(WebApplicationFactory<Program> host)
        => host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, string password = SongVaultApiFactory.TestPassword)
        => client.PostAsJsonAsync("/api/me/delete", new { password });

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email)
        => client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = SongVaultApiFactory.TestPassword });

    /// <summary>Un Owner, son groupe « Les Rats », et un membre.</summary>
    private async Task<(HttpClient Owner, Guid BandId, HttpClient Member, string MemberEmail)> SharedBandAsync()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var created = await owner.PostAsJsonAsync("/api/bands", new { name = "Les Rats" });
        var band = (await created.Content.ReadFromJsonAsync<BandResponse>(TestJson.Options))!;
        var memberEmail = NewEmail();
        var member = await factory.CreateNewUserClientAsync(memberEmail);
        await factory.AddMemberAsync(band.Id, member, BandRole.Member);
        return (owner, band.Id, member, memberEmail);
    }

    [Fact]
    public async Task Un_mauvais_mot_de_passe_ne_supprime_rien()
    {
        var email = NewEmail();
        var client = await factory.CreateNewUserClientAsync(email);

        var response = await DeleteAsync(client, "Pas-le-bon1!");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(Anonymous(factory), email)).StatusCode);
    }

    [Fact]
    public async Task Le_dernier_Owner_d_un_groupe_partage_doit_d_abord_passer_la_main()
    {
        var (owner, bandId, _, _) = await SharedBandAsync();

        var response = await DeleteAsync(owner);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("« Les Rats »", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
        var members = await owner.GetFromJsonAsync<List<BandMemberResponse>>($"/api/bands/{bandId}/members", TestJson.Options);
        Assert.Equal(2, members!.Count);                                       // rien n'a changé
    }

    [Fact]
    public async Task Un_membre_supprime_quitte_ses_groupes_et_ne_peut_plus_se_connecter()
    {
        var (owner, bandId, member, memberEmail) = await SharedBandAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(member)).StatusCode);

        var login = await LoginAsync(Anonymous(factory), memberEmail);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal("LockedOut", (await login.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
        var members = await owner.GetFromJsonAsync<List<BandMemberResponse>>($"/api/bands/{bandId}/members", TestJson.Options);
        Assert.DoesNotContain(members!, m => m.Email == memberEmail);
    }

    [Fact]
    public async Task La_purge_a_J30_efface_le_compte_son_groupe_et_ses_fichiers()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var host = factory.WithClock(clock);
        var email = NewEmail();
        var client = await host.CreateNewUserClientAsync(email);
        var userId = await client.UserIdAsync();
        var bandId = await client.FirstBandIdAsync();
        var (songId, _) = await client.CreateSongWithFileAsync("Purge", TestFiles.Mp3(1024));
        var keys = await FileKeysAsync(host, songId);

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(client)).StatusCode);

        clock.Advance(TimeSpan.FromDays(29));
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);
        Assert.True(await UserExistsAsync(host, userId));                      // pas avant J+30

        clock.Advance(TimeSpan.FromDays(1));
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
        Assert.False(await UserExistsAsync(host, userId));
        Assert.False(await db.Bands.AnyAsync(b => b.Id == bandId));
        Assert.False(await db.Songs.IgnoreQueryFilters([SongVaultDbContext.BandFilter]).AnyAsync(s => s.Id == songId));
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        foreach (var key in keys)
            await Assert.ThrowsAsync<FileNotFoundException>(() => storage.OpenReadAsync(key, CancellationToken.None));

        // L'adresse est libre à nouveau
        var again = await Anonymous(host).PostAsJsonAsync("/api/auth/register",
            new { email, password = SongVaultApiFactory.TestPassword });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    private static async Task<List<string>> FileKeysAsync(WebApplicationFactory<Program> host, Guid songId)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SongVaultDbContext>()
            .Songs.IgnoreQueryFilters([SongVaultDbContext.BandFilter])
            .Where(s => s.Id == songId)
            .SelectMany(s => s.Versions).SelectMany(v => v.Files)
            .Select(f => f.StorageKey)
            .ToListAsync();
    }

    private static async Task<bool> UserExistsAsync(WebApplicationFactory<Program> host, string userId)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SongVaultDbContext>().Users.AnyAsync(u => u.Id == userId);
    }
}