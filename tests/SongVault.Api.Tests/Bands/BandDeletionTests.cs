using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Api.Contracts.Bands;
using SongVault.Api.Tests.Accounts;
using SongVault.Api.Tests.Billing;
using SongVault.Application.Abstractions;
using SongVault.Domain.Bands;
using SongVault.Domain.Billing;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests.Bands;

/// <summary>Suppression d'un groupe par son Owner (ADR 0048).</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public sealed class BandDeletionTests(SongVaultApiFactory factory)
{
    private static Task<HttpResponseMessage> DeleteBandAsync(HttpClient client, Guid bandId, string confirmName)
        => client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/bands/{bandId}")
        {
            Content = JsonContent.Create(new { confirmName }),
        });

    private static async Task<List<BandResponse>> MineAsync(HttpClient client)
        => (await client.GetFromJsonAsync<List<BandResponse>>("/api/bands", TestJson.Options))!;

    /// <summary>Un nouvel Owner et son groupe, seul groupe du compte.</summary>
    private static async Task<Guid> CreateBandAsync(HttpClient owner, string name)
    {
        var created = await owner.PostAsJsonAsync("/api/bands", new { name });
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<BandResponse>(TestJson.Options))!.Id;
    }

    [Fact]
    public async Task L_Owner_supprime_le_groupe_avec_ses_morceaux_sa_corbeille_ses_sorties_et_ses_fichiers()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await CreateBandAsync(owner, "Mauvais compte");
        var member = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, member, BandRole.Member);
        var (songId, _) = await owner.CreateSongWithFileAsync("Nocturne", TestFiles.Mp3(2048));
        var (trashedId, _) = await owner.CreateSongWithFileAsync("À jeter", TestFiles.Mp3(1024));
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/bands/{bandId}/songs/{trashedId}")).StatusCode);
        (await owner.PostAsJsonAsync($"/api/bands/{bandId}/releases", new { title = "EP", type = "EP" })).EnsureSuccessStatusCode();
        (await owner.PostAsJsonAsync($"/api/bands/{bandId}/setlists", new { name = "Concert" })).EnsureSuccessStatusCode();
        (await owner.PutAsJsonAsync($"/api/bands/{bandId}", new { name = "Mauvais compte !" })).EnsureSuccessStatusCode();
        var keys = await FileKeysAsync(bandId);
        Assert.Equal(2, keys.Count);

        var response = await DeleteBandAsync(owner, bandId, "  Mauvais compte !  ");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await MineAsync(owner));                                  // le front l'envoie sur Bienvenue
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/bands/{bandId}")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
        Assert.False(await db.Bands.AnyAsync(b => b.Id == bandId));
        Assert.False(await db.Songs.IgnoreQueryFilters().AnyAsync(s => s.Id == songId || s.Id == trashedId));
        Assert.False(await db.Releases.IgnoreQueryFilters().AnyAsync(r => r.BandId == bandId));
        Assert.False(await db.Setlists.IgnoreQueryFilters().AnyAsync(s => s.BandId == bandId));
        Assert.False(await db.AuditEntries.AnyAsync(e => e.BandId == bandId));   // le déclencheur laisse passer
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        foreach (var key in keys)
            await Assert.ThrowsAsync<FileNotFoundException>(() => storage.OpenReadAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task Un_nom_mal_recopie_ne_supprime_rien()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await CreateBandAsync(owner, "Les Rats");

        var response = await DeleteBandAsync(owner, bandId, "les rats");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Recopiez exactement le nom du groupe",
            (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestJson.Options))!.Detail);
        Assert.Single(await MineAsync(owner));
    }

    [Theory]
    [InlineData(BandRole.Member)]
    [InlineData(BandRole.Guest)]
    public async Task Seul_un_Owner_peut_supprimer_le_groupe(BandRole role)
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await CreateBandAsync(owner, "Les Rats");
        var other = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, other, role);

        Assert.Equal(HttpStatusCode.Forbidden, (await DeleteBandAsync(other, bandId, "Les Rats")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await DeleteBandAsync(await factory.CreateNewUserClientAsync(), bandId, "Les Rats")).StatusCode);
        Assert.Single(await MineAsync(owner));
    }

    [Fact]
    public async Task Un_abonnement_en_cours_bloque_la_suppression()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await CreateBandAsync(owner, "Abonnés");
        await AddStripeSubscriptionAsync(bandId, $"sub_{Guid.NewGuid():N}", cancelAtPeriodEnd: false);

        var response = await DeleteBandAsync(owner, bandId, "Abonnés");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("résiliez-le depuis la page Facturation",
            (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestJson.Options))!.Detail);
        Assert.Single(await MineAsync(owner));
    }

    [Fact]
    public async Task Un_abonnement_resilie_pour_la_fin_de_la_periode_s_arrete_avec_le_groupe()
    {
        var payments = new TestPaymentGateway();
        await using var host = factory.WithPayments(payments);
        var owner = await host.CreateNewUserClientAsync();
        var bandId = await CreateBandAsync(owner, "Abonnés");
        var subscriptionId = $"sub_{Guid.NewGuid():N}";
        await AddStripeSubscriptionAsync(bandId, subscriptionId, cancelAtPeriodEnd: true);

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteBandAsync(owner, bandId, "Abonnés")).StatusCode);

        Assert.Equal(new[] { subscriptionId }, payments.Cancelled);
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<SongVaultDbContext>()
            .Subscriptions.AnyAsync(s => s.BandId == bandId));
    }

    private async Task AddStripeSubscriptionAsync(Guid bandId, string subscriptionId, bool cancelAtPeriodEnd)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
        db.Subscriptions.Add(Subscription.FromStripe(bandId, new SubscriptionState(
            subscriptionId, "cus_1", bandId, "band", SubscriptionStatus.Active,
            DateTimeOffset.UtcNow.AddDays(30), cancelAtPeriodEnd), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    private async Task<List<string>> FileKeysAsync(Guid bandId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SongVaultDbContext>()
            .Songs.IgnoreQueryFilters()
            .Where(s => s.BandId == bandId)
            .SelectMany(s => s.Versions).SelectMany(v => v.Files)
            .Select(f => f.StorageKey)
            .ToListAsync();
    }
}
