using SongVault.Domain.Billing;
using SongVault.Domain.Common;

namespace SongVault.Domain.Tests.Billing;

public sealed class PlanTests
{
    [Theory]
    [InlineData(0, 100, true)]
    [InlineData(900, 100, true)]           // pile le quota
    [InlineData(900, 101, false)]
    [InlineData(1200, 1, false)]           // déjà au-delà (formule descendue) : plus rien n'entre
    public void Un_fichier_entre_tant_que_le_quota_n_est_pas_depasse(long used, long incoming, bool expected)
    {
        var plan = Plan.Create("mini", "Mini", 1000, maxBandsPerOwner: null, monthlyPriceCents: 0, sortOrder: 1);

        Assert.Equal(expected, plan.HasRoomFor(used, incoming));
    }

    [Fact]
    public void Refuse_un_quota_nul_ou_un_prix_negatif()
    {
        Assert.Throws<DomainException>(() => Plan.Create("x", "X", 0, null, 0, 1));
        Assert.Throws<DomainException>(() => Plan.Create("x", "X", 1000, null, -1, 1));
        Assert.Throws<DomainException>(() => Plan.Create("x", "X", 1000, 0, 0, 1));
    }
}