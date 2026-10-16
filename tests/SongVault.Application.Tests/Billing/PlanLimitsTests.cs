using SongVault.Application.Billing;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Billing;

namespace SongVault.Application.Tests.Billing;

public sealed class PlanLimitsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.CreateVersion7();
    private readonly FakeBillingRepository _billing = new();

    private PlanLimits Sut() => new(_billing);

    private static SubscriptionState Stripe(string planId, SubscriptionStatus status)
        => new("sub_1", "cus_1", BandId, planId, status, Now.AddDays(30), CancelAtPeriodEnd: false);

    [Fact]
    public async Task Sans_abonnement_le_groupe_est_sur_la_formule_gratuite()
    {
        _billing.StorageUsed[BandId] = 300;

        var usage = await Sut().GetUsageAsync(BandId, CancellationToken.None);

        Assert.Equal(new BandUsageDto(Plan.FreeId, "Gratuit", 300, 1000), usage);
    }

    [Fact]
    public async Task Un_abonnement_en_cours_donne_le_quota_de_sa_formule()
    {
        _billing.StorageUsed[BandId] = 300;
        _billing.Subscriptions.Add(Subscription.FromStripe(BandId, Stripe("band", SubscriptionStatus.PastDue), Now));

        var usage = await Sut().GetUsageAsync(BandId, CancellationToken.None);

        Assert.Equal(("band", 10_000L), (usage.PlanId, usage.QuotaBytes));   // PastDue : la formule reste
    }

    [Fact]
    public async Task Un_abonnement_resilie_repasse_en_gratuit_sans_rien_effacer()
    {
        _billing.StorageUsed[BandId] = 5000;                                   // rempli du temps de la formule Groupe
        _billing.Subscriptions.Add(Subscription.FromStripe(BandId, Stripe("band", SubscriptionStatus.Canceled), Now));

        var usage = await Sut().GetUsageAsync(BandId, CancellationToken.None);
        var error = await Assert.ThrowsAsync<PlanLimitException>(
            () => Sut().EnsureStorageAsync(BandId, 1, CancellationToken.None));

        Assert.Equal((Plan.FreeId, 5000L), (usage.PlanId, usage.UsedBytes));
        Assert.Contains("formule Gratuit", error.Message);
    }

    [Fact]
    public async Task Accepte_un_fichier_qui_remplit_pile_le_quota()
    {
        _billing.StorageUsed[BandId] = 900;

        await Sut().EnsureStorageAsync(BandId, 100, CancellationToken.None);
    }

    [Fact]
    public async Task Refuse_un_fichier_de_trop_avec_les_tailles_lisibles()
    {
        _billing.StorageUsed[BandId] = 900;

        var error = await Assert.ThrowsAsync<PlanLimitException>(
            () => Sut().EnsureStorageAsync(BandId, 101, CancellationToken.None));

        Assert.Equal(
            "Stockage du groupe plein : 900 o utilisés sur 1000 o avec la formule Gratuit, et ce fichier fait 101 o. " +
            "Supprimez des fichiers ou passez à une formule supérieure.", error.Message);
    }

    [Fact]
    public async Task Un_groupe_inconnu_leve_NotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Sut().EnsureStorageAsync(BandId, 1, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => Sut().GetUsageAsync(BandId, CancellationToken.None));
    }

    [Fact]
    public async Task Un_premier_groupe_gratuit_se_cree_le_deuxieme_non()
    {
        await Sut().EnsureCanCreateBandAsync("owner-1", CancellationToken.None);

        _billing.OwnedFreeBands = 1;
        var error = await Assert.ThrowsAsync<PlanLimitException>(
            () => Sut().EnsureCanCreateBandAsync("owner-1", CancellationToken.None));

        Assert.StartsWith("La formule Gratuit permet d'être propriétaire de 1 groupe sans abonnement.", error.Message);
    }
}