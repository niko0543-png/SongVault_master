using System.Net;
using System.Net.Http.Json;

using SongVault.Api.Contracts.Billing;
using SongVault.Domain.Billing;

namespace SongVault.Api.Tests.Billing;

[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public sealed class StripeWebhookTests(SongVaultApiFactory factory)
{
    private static SubscriptionState State(string id, Guid bandId, string planId, SubscriptionStatus status)
        => new(id, $"cus_{id}", bandId, planId, status, DateTimeOffset.UtcNow.AddDays(30), CancelAtPeriodEnd: false);

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    [Fact]
    public async Task Un_paiement_reussi_passe_le_groupe_sur_sa_formule()
    {
        var payments = new TestPaymentGateway();
        await using var host = factory.WithPayments(payments);
        var client = await host.CreateNewUserClientAsync();
        var bandId = await client.FirstBandIdAsync();
        var subscriptionId = NewId("sub");
        payments.States[subscriptionId] = State(subscriptionId, bandId, "band", SubscriptionStatus.Active);

        var webhook = host.CreateClient();
        var response = await webhook.PostStripeEventAsync(
            StripeTestHelpers.StripeEvent(NewId("evt"), "checkout.session.completed", subscriptionId, bandId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var billing = await client.GetFromJsonAsync<BillingResponse>($"/api/bands/{bandId}/billing", TestJson.Options);
        Assert.Equal(("band", SubscriptionStatus.Active), (billing!.Usage.PlanId, billing.Status));
        Assert.True(billing.CanOpenPortal);
        Assert.False(billing.CanCheckout);
    }

    [Fact]
    public async Task Un_evenement_rejoue_ne_change_rien_la_seconde_fois()
    {
        var payments = new TestPaymentGateway();
        await using var host = factory.WithPayments(payments);
        var client = await host.CreateNewUserClientAsync();
        var bandId = await client.FirstBandIdAsync();
        var subscriptionId = NewId("sub");
        payments.States[subscriptionId] = State(subscriptionId, bandId, "band", SubscriptionStatus.Active);
        var payload = StripeTestHelpers.StripeEvent(NewId("evt"), "customer.subscription.created", subscriptionId, bandId);
        var webhook = host.CreateClient();

        await webhook.PostStripeEventAsync(payload);
        payments.States[subscriptionId] = State(subscriptionId, bandId, "band", SubscriptionStatus.Canceled);
        var replay = await webhook.PostStripeEventAsync(payload);              // Stripe renvoie le même

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var usage = await client.GetFromJsonAsync<UsageResponse>($"/api/bands/{bandId}/usage", TestJson.Options);
        Assert.Equal("band", usage!.PlanId);                                   // déjà traité : pas relu
    }

    [Fact]
    public async Task Un_paiement_refuse_ecrit_aux_Owner()
    {
        var payments = new TestPaymentGateway();
        await using var host = factory.WithPayments(payments);
        var email = $"owner-{Guid.NewGuid():N}@test.local";
        var client = await host.CreateNewUserClientAsync(email);
        var bandId = await client.FirstBandIdAsync();
        var subscriptionId = NewId("sub");
        payments.States[subscriptionId] = State(subscriptionId, bandId, "band", SubscriptionStatus.PastDue);

        await host.CreateClient().PostStripeEventAsync(
            StripeTestHelpers.StripeEvent(NewId("evt"), "invoice.payment_failed", subscriptionId));
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);

        var mail = factory.Emails.LastTo(email);
        Assert.StartsWith("Paiement refusé pour", mail.Subject);
        Assert.Contains($"http://localhost:8080/b/{bandId}/billing", mail.TextBody);
    }

    [Theory]
    [InlineData("whsec_un_autre_secret", 0)]                               // signé par quelqu'un d'autre
    [InlineData(StripeTestHelpers.WebhookSecret, -10)]                 // signé il y a 10 minutes : rejoué
    public async Task Une_signature_fausse_ou_trop_vieille_repond_400(string secret, int minutes)
    {
        var payments = new TestPaymentGateway();
        await using var host = factory.WithPayments(payments);
        var bandId = Guid.NewGuid();
        var subscriptionId = NewId("sub");
        payments.States[subscriptionId] = State(subscriptionId, bandId, "studio", SubscriptionStatus.Active);

        var response = await host.CreateClient().PostStripeEventAsync(
            StripeTestHelpers.StripeEvent(NewId("evt"), "customer.subscription.created", subscriptionId, bandId),
            secret, DateTimeOffset.UtcNow.AddMinutes(minutes));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sans_en_tete_de_signature_repond_400()
    {
        var response = await factory.CreateClient().PostAsync("/api/billing/stripe/webhook",
            new StringContent("""{"id":"evt_1","type":"customer.subscription.created","data":{"object":{}}}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}