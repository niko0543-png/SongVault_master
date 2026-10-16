using SongVault.Domain.Billing;
using SongVault.Domain.Common;

namespace SongVault.Domain.Tests.Billing;

public sealed class SubscriptionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.NewGuid();

    private static SubscriptionState State(string id = "sub_1", SubscriptionStatus status = SubscriptionStatus.Active,
        string? planId = "band", bool cancelAtPeriodEnd = false)
        => new(id, "cus_1", BandId, planId, status, Now.AddMonths(1), cancelAtPeriodEnd);

    [Theory]
    [InlineData(SubscriptionStatus.Active, "band")]
    [InlineData(SubscriptionStatus.Trialing, "band")]
    [InlineData(SubscriptionStatus.PastDue, "band")]
    [InlineData(SubscriptionStatus.Incomplete, Plan.FreeId)]
    [InlineData(SubscriptionStatus.IncompleteExpired, Plan.FreeId)]
    [InlineData(SubscriptionStatus.Canceled, Plan.FreeId)]
    [InlineData(SubscriptionStatus.Unpaid, Plan.FreeId)]
    [InlineData(SubscriptionStatus.Paused, Plan.FreeId)]
    public void La_formule_payee_s_applique_tant_que_l_abonnement_court(SubscriptionStatus status, string expected)
    {
        var subscription = Subscription.FromStripe(BandId, State(status: status), Now);

        Assert.Equal(expected, subscription.EffectivePlanId);
    }

    [Fact]
    public void Garde_ce_que_Stripe_decrit()
    {
        var subscription = Subscription.FromStripe(BandId, State(cancelAtPeriodEnd: true), Now);

        Assert.Equal(BandId, subscription.BandId);
        Assert.Equal("band", subscription.PlanId);
        Assert.Equal("cus_1", subscription.StripeCustomerId);
        Assert.Equal("sub_1", subscription.StripeSubscriptionId);
        Assert.Equal(Now.AddMonths(1), subscription.CurrentPeriodEnd);
        Assert.True(subscription.CancelAtPeriodEnd);
        Assert.True(subscription.IsLiveOnStripe);
    }

    [Fact]
    public void Un_changement_de_formule_remplace_la_precedente()
    {
        var subscription = Subscription.FromStripe(BandId, State(), Now);

        Assert.True(subscription.Apply(State(planId: "studio"), Now.AddDays(3)));

        Assert.Equal("studio", subscription.EffectivePlanId);
        Assert.Equal(Now.AddDays(3), subscription.UpdatedAt);
    }

    [Fact]
    public void Un_ancien_abonnement_arrete_n_efface_pas_celui_qui_court()
    {
        var subscription = Subscription.FromStripe(BandId, State("sub_new"), Now);

        var applied = subscription.Apply(State("sub_old", SubscriptionStatus.Canceled), Now.AddMinutes(1));

        Assert.False(applied);
        Assert.Equal("sub_new", subscription.StripeSubscriptionId);
        Assert.Equal("band", subscription.EffectivePlanId);
    }

    [Fact]
    public void Un_nouvel_abonnement_remplace_un_ancien_arrete()
    {
        var subscription = Subscription.FromStripe(BandId, State("sub_old", SubscriptionStatus.Canceled), Now);

        Assert.True(subscription.Apply(State("sub_new"), Now.AddDays(1)));

        Assert.Equal("sub_new", subscription.StripeSubscriptionId);
        Assert.Equal("band", subscription.EffectivePlanId);
    }

    [Fact]
    public void Refuse_un_prix_qui_ne_correspond_a_aucune_formule()
    {
        Assert.Throws<DomainException>(() => Subscription.FromStripe(BandId, State(planId: null), Now));
    }

    [Fact]
    public void Une_formule_offerte_s_applique_sans_Stripe()
    {
        var subscription = Subscription.Grant(BandId, "studio", Now);

        Assert.Equal("studio", subscription.EffectivePlanId);
        Assert.Null(subscription.StripeSubscriptionId);
        Assert.False(subscription.IsLiveOnStripe);       // un paiement peut la remplacer
    }

    [Fact]
    public void On_n_offre_pas_la_formule_gratuite()
    {
        Assert.Throws<DomainException>(() => Subscription.Grant(BandId, Plan.FreeId, Now));
    }

    [Fact]
    public void Un_paiement_remplace_une_formule_offerte()
    {
        var subscription = Subscription.Grant(BandId, "band", Now);

        Assert.True(subscription.Apply(State(planId: "studio"), Now.AddDays(1)));

        Assert.Equal("sub_1", subscription.StripeSubscriptionId);
        Assert.Equal("studio", subscription.EffectivePlanId);
    }

    [Fact]
    public void Un_evenement_Stripe_a_un_identifiant_et_un_type()
    {
        var processed = ProcessedStripeEvent.Create("evt_1", "invoice.payment_failed", Now);

        Assert.Equal("evt_1", processed.EventId);
        Assert.Throws<DomainException>(() => ProcessedStripeEvent.Create(" ", "invoice.payment_failed", Now));
    }
}