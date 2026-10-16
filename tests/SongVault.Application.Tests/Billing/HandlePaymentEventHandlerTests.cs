using SongVault.Application.Abstractions;
using SongVault.Application.Billing;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Billing;

namespace SongVault.Application.Tests.Billing;

public sealed class HandlePaymentEventHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.CreateVersion7();
    private readonly FakeBillingRepository _billing = new();
    private readonly FakePaymentGateway _payments = new();
    private readonly FakeBackgroundJobs _jobs = new();
    private readonly FakeUnitOfWork _uow = new();

    public HandlePaymentEventHandlerTests() => _billing.StorageUsed[BandId] = 0;

    private HandlePaymentEventHandler Sut() => new(_billing, _payments, _jobs, _uow, new FixedTimeProvider(Now));

    private void OnStripe(SubscriptionStatus status, string planId = "band", Guid? bandId = null)
        => _payments.Subscriptions["sub_1"] = new SubscriptionState(
            "sub_1", "cus_1", bandId ?? BandId, planId, status, Now.AddDays(30), CancelAtPeriodEnd: false);

    private Task<PaymentEventOutcome> Handle(string id, string type, Guid? bandHint = null)
        => Sut().HandleAsync(new PaymentEvent(id, type, "sub_1", bandHint), CancellationToken.None);

    [Fact]
    public async Task Un_paiement_reussi_cree_l_abonnement_du_groupe()
    {
        OnStripe(SubscriptionStatus.Active);

        var outcome = await Handle("evt_1", HandlePaymentEventHandler.CheckoutCompleted);

        Assert.Equal(PaymentEventOutcome.Applied, outcome);
        var subscription = Assert.Single(_billing.Subscriptions);
        Assert.Equal((BandId, "band", "sub_1", "cus_1"),
            (subscription.BandId, subscription.PlanId, subscription.StripeSubscriptionId, subscription.StripeCustomerId));
        Assert.Contains("evt_1", _billing.Processed);
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Le_meme_evenement_ne_passe_qu_une_fois()
    {
        OnStripe(SubscriptionStatus.Active);
        await Handle("evt_1", "customer.subscription.created");

        var outcome = await Handle("evt_1", "customer.subscription.created");

        Assert.Equal(PaymentEventOutcome.Duplicate, outcome);
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task L_etat_vient_toujours_de_Stripe_meme_pour_un_evenement_en_retard()
    {
        OnStripe(SubscriptionStatus.Active);
        await Handle("evt_1", "customer.subscription.created");
        OnStripe(SubscriptionStatus.Active, planId: "studio");                 // déjà passé en Studio chez Stripe

        await Handle("evt_0", "customer.subscription.updated");               // vieil événement arrivé après

        Assert.Equal("studio", Assert.Single(_billing.Subscriptions).PlanId);
    }

    [Fact]
    public async Task Une_resiliation_repasse_le_groupe_en_gratuit()
    {
        OnStripe(SubscriptionStatus.Active);
        await Handle("evt_1", "customer.subscription.created");
        OnStripe(SubscriptionStatus.Canceled);

        await Handle("evt_2", "customer.subscription.deleted");

        Assert.Equal(Plan.FreeId, Assert.Single(_billing.Subscriptions).EffectivePlanId);
    }

    [Fact]
    public async Task Un_paiement_refuse_previent_les_Owner()
    {
        OnStripe(SubscriptionStatus.PastDue);

        await Handle("evt_1", HandlePaymentEventHandler.PaymentFailed);

        Assert.Equal((PaymentFailedJob.JobType, BandId.ToString(), Now), Assert.Single(_jobs.Enqueued));
        Assert.Equal("band", Assert.Single(_billing.Subscriptions).EffectivePlanId);   // la formule reste
    }

    [Theory]
    [InlineData("charge.succeeded")]                                       // non coché, mais envoyé quand même
    [InlineData("customer.subscription.updated")]                          // abonnement effacé chez Stripe
    public async Task Ignore_un_evenement_hors_sujet(string type)
    {
        var outcome = await Handle("evt_1", type);

        Assert.Equal(PaymentEventOutcome.Ignored, outcome);
        Assert.Empty(_billing.Subscriptions);
        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task Ignore_un_groupe_purge_depuis()
    {
        OnStripe(SubscriptionStatus.Active, bandId: Guid.CreateVersion7());

        var outcome = await Handle("evt_1", "customer.subscription.updated");

        Assert.Equal(PaymentEventOutcome.Ignored, outcome);
        Assert.Empty(_billing.Subscriptions);
    }

    [Fact]
    public async Task Sans_band_id_sur_l_abonnement_le_groupe_vient_de_la_session_de_paiement()
    {
        _payments.Subscriptions["sub_1"] = new SubscriptionState(
            "sub_1", "cus_1", null, "band", SubscriptionStatus.Active, Now.AddDays(30), false);

        await Handle("evt_1", HandlePaymentEventHandler.CheckoutCompleted, bandHint: BandId);

        Assert.Equal(BandId, Assert.Single(_billing.Subscriptions).BandId);
    }

    [Fact]
    public async Task Un_paiement_remplace_une_formule_offerte()
    {
        _billing.Subscriptions.Add(Subscription.Grant(BandId, "studio", Now.AddDays(-10)));
        OnStripe(SubscriptionStatus.Active);

        await Handle("evt_1", HandlePaymentEventHandler.CheckoutCompleted);

        var subscription = Assert.Single(_billing.Subscriptions);
        Assert.Equal(("band", "sub_1"), (subscription.PlanId, subscription.StripeSubscriptionId));
    }
}