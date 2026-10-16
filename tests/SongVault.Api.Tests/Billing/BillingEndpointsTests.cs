using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Api.Contracts.Billing;
using SongVault.Api.Contracts.Files;
using SongVault.Api.Tests.Files;
using SongVault.Domain.Bands;
using SongVault.Domain.Billing;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests.Billing;

[Collection(ApiCollection.Name)]
public sealed class BillingEndpointsTests(SongVaultApiFactory factory)
{
    [Fact]
    public async Task Les_formules_se_lisent_sans_compte()
    {
        var anonymous = factory.CreateClient();

        var catalog = await anonymous.GetFromJsonAsync<PlanCatalogResponse>("/api/plans", TestJson.Options);

        Assert.False(catalog!.OnlinePayment);                               // Billing:Provider None
        Assert.Equal(["free", "band", "studio"], catalog.Plans.Take(3).Select(p => p.Id));
        Assert.Equal(2L * 1024 * 1024 * 1024, catalog.Plans[0].StorageQuotaBytes);
    }

    [Fact]
    public async Task La_jauge_donne_la_formule_et_le_stockage_du_groupe()
    {
        var client = await factory.CreateNewUserClientAsync();
        var bandId = await client.FirstBandIdAsync();
        var versionUrl = await client.CreateVersionUrlAsync();

        var upload = await client.PostAsync($"{versionUrl}/files", TestFiles.Form(TestFiles.Mp3(4096), "a.mp3"));
        var file = (await upload.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!;
        var after = await client.GetFromJsonAsync<UsageResponse>($"/api/bands/{bandId}/usage", TestJson.Options);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{versionUrl}/files/{file.Id}")).StatusCode);
        var deleted = await client.GetFromJsonAsync<UsageResponse>($"/api/bands/{bandId}/usage", TestJson.Options);

        Assert.Equal(("free", 4096L), (after!.PlanId, after.UsedBytes));
        Assert.Equal(0, deleted!.UsedBytes);
    }

    [Fact]
    public async Task Un_Guest_lit_la_jauge_mais_pas_la_facturation()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var guest = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        await factory.AddMemberAsync(bandId, guest, BandRole.Guest);

        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"/api/bands/{bandId}/usage")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.GetAsync($"/api/bands/{bandId}/billing")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await guest.PostAsJsonAsync($"/api/bands/{bandId}/billing/checkout", new { planId = "band" })).StatusCode);
    }

    [Fact]
    public async Task Sans_paiement_en_ligne_la_facturation_s_affiche_et_le_paiement_repond_503()
    {
        var client = await factory.CreateNewUserClientAsync();
        var bandId = await client.FirstBandIdAsync();

        var billing = await client.GetFromJsonAsync<BillingResponse>($"/api/bands/{bandId}/billing", TestJson.Options);
        var checkout = await client.PostAsJsonAsync($"/api/bands/{bandId}/billing/checkout", new { planId = "band" });

        Assert.False(billing!.OnlinePayment);
        Assert.False(billing.CanCheckout);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, checkout.StatusCode);
        var problem = await checkout.Content.ReadFromJsonAsync<ProblemDetails>(TestJson.Options);
        Assert.Equal("Le paiement en ligne n'est pas encore ouvert.", problem!.Detail);
    }

    [Fact]
    public async Task Le_paiement_renvoie_l_adresse_Stripe_Checkout()
    {
        var payments = new TestPaymentGateway();
        await using var host = factory.WithPayments(payments);
        var client = await host.CreateNewUserClientAsync();
        var bandId = await client.FirstBandIdAsync();

        var response = await client.PostAsJsonAsync($"/api/bands/{bandId}/billing/checkout", new { planId = "studio" });

        var redirect = await response.Content.ReadFromJsonAsync<RedirectResponse>(TestJson.Options);
        Assert.Equal("https://checkout.stripe.test/c/1", redirect!.Url.ToString());
        var request = Assert.Single(payments.Checkouts);
        Assert.Equal((bandId, "studio"), (request.BandId, request.PlanId));
        Assert.Equal($"http://localhost:8080/b/{bandId}/billing?checkout=success", request.SuccessUrl.ToString());
    }

    [Fact]
    public async Task Supprimer_son_compte_est_refuse_tant_qu_un_abonnement_court()
    {
        var client = await factory.CreateNewUserClientAsync();
        var bandId = await client.FirstBandIdAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
            db.Subscriptions.Add(Subscription.FromStripe(bandId, new SubscriptionState(
                $"sub_{Guid.NewGuid():N}", "cus_1", bandId, "band", SubscriptionStatus.Active,
                DateTimeOffset.UtcNow.AddDays(30), CancelAtPeriodEnd: false), DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/me/delete", new { password = SongVaultApiFactory.TestPassword });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestJson.Options);
        Assert.Contains("a un abonnement en cours", problem!.Detail);
    }
}