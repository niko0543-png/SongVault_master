using Microsoft.Extensions.Options;

using SongVault.Application.Billing;
using SongVault.Application.Common;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Billing;
using SongVault.Domain.Common;

namespace SongVault.Application.Tests.Billing;

public sealed class BillingHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.CreateVersion7();
    private static readonly IOptions<AppOptions> App = Options.Create(new AppOptions { PublicUrl = "https://songvault.example/" });
    private readonly FakeBillingRepository _billing = new();
    private FakePaymentGateway _payments = new();

    public BillingHandlersTests() => _billing.StorageUsed[BandId] = 250;

    private StartCheckoutHandler Checkout()
        => new(_billing, _payments, new FakeBandContext(BandId), new FakeCurrentUser(), App);

    private GetBillingHandler Billing() => new(_billing, new PlanLimits(_billing), _payments, new FakeBandContext(BandId));

    private void Paid(SubscriptionStatus status = SubscriptionStatus.Active) => _billing.Subscriptions.Add(Subscription.FromStripe(
        BandId, new SubscriptionState("sub_1", "cus_1", BandId, "band", status, Now.AddDays(30), false), Now));

    [Fact]
    public async Task Les_formules_sortent_dans_l_ordre_avec_l_etat_du_paiement()
    {
        var catalog = await new ListPlansHandler(_billing, new FakePaymentGateway(enabled: false)).HandleAsync(CancellationToken.None);

        Assert.False(catalog.OnlinePayment);
        Assert.Equal([Plan.FreeId, "band", "studio"], catalog.Plans.Select(p => p.Id));
    }

    [Fact]
    public async Task Le_paiement_part_vers_Stripe_avec_le_groupe_et_les_adresses_de_retour()
    {
        var url = await Checkout().HandleAsync("band", CancellationToken.None);

        Assert.Equal("https://checkout.stripe.test/c/1", url.ToString());
        var request = Assert.Single(_payments.Checkouts);
        Assert.Equal((BandId, "band", null, "owner-1@test.local"),
            (request.BandId, request.PlanId, request.CustomerId, request.CustomerEmail));
        Assert.Equal($"https://songvault.example/b/{BandId}/billing?checkout=success", request.SuccessUrl.ToString());
        Assert.Equal($"https://songvault.example/b/{BandId}/billing", request.CancelUrl.ToString());
    }

    [Fact]
    public async Task Un_ancien_client_Stripe_est_repris_apres_une_resiliation()
    {
        Paid(SubscriptionStatus.Canceled);

        await Checkout().HandleAsync("studio", CancellationToken.None);

        Assert.Equal("cus_1", Assert.Single(_payments.Checkouts).CustomerId);   // mêmes factures, même carte
    }

    [Fact]
    public async Task Refuse_un_second_abonnement_la_formule_gratuite_et_une_formule_inconnue()
    {
        await Assert.ThrowsAsync<DomainException>(() => Checkout().HandleAsync(Plan.FreeId, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => Checkout().HandleAsync("platine", CancellationToken.None));
        Paid();
        await Assert.ThrowsAsync<ConflictException>(() => Checkout().HandleAsync("studio", CancellationToken.None));

        Assert.Empty(_payments.Checkouts);
    }

    [Fact]
    public async Task Sans_paiement_en_ligne_le_paiement_repond_indisponible()
    {
        _payments = new FakePaymentGateway(enabled: false);

        await Assert.ThrowsAsync<BillingUnavailableException>(() => Checkout().HandleAsync("band", CancellationToken.None));
    }

    [Fact]
    public async Task Facturation_d_un_groupe_gratuit()
    {
        var billing = await Billing().HandleAsync(CancellationToken.None);

        Assert.Equal(new BandUsageDto(Plan.FreeId, "Gratuit", 250, 1000), billing.Usage);
        Assert.Null(billing.Status);
        Assert.True(billing.CanCheckout);
        Assert.False(billing.CanOpenPortal);
        Assert.False(billing.IsGranted);
    }

    [Fact]
    public async Task Facturation_d_un_groupe_abonne()
    {
        Paid();

        var billing = await Billing().HandleAsync(CancellationToken.None);

        Assert.Equal(("band", SubscriptionStatus.Active), (billing.Usage.PlanId, billing.Status));
        Assert.Equal(Now.AddDays(30), billing.CurrentPeriodEnd);
        Assert.False(billing.CanCheckout);
        Assert.True(billing.CanOpenPortal);
    }

    [Fact]
    public async Task Facturation_d_une_formule_offerte()
    {
        _billing.Subscriptions.Add(Subscription.Grant(BandId, "studio", Now));

        var billing = await Billing().HandleAsync(CancellationToken.None);

        Assert.Equal("studio", billing.Usage.PlanId);
        Assert.True(billing.IsGranted);
        Assert.False(billing.CanOpenPortal);                                  // rien n'a été payé
    }

    [Fact]
    public async Task Le_portail_s_ouvre_pour_le_client_du_groupe()
    {
        var portal = new OpenBillingPortalHandler(_billing, _payments, new FakeBandContext(BandId), App);
        await Assert.ThrowsAsync<ConflictException>(() => portal.HandleAsync(CancellationToken.None));

        Paid();
        var url = await portal.HandleAsync(CancellationToken.None);

        Assert.Equal("https://billing.stripe.test/p/cus_1", url.ToString());
    }
}