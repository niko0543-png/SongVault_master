using SongVault.Domain.Common;
using SongVault.Domain.Sharing;

namespace SongVault.Domain.Tests.Sharing;

public sealed class ShareLinkTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Hash = new byte[ShareLink.TokenHashLength];

    private static ShareLink ForVersion(int days = 30, string? label = null, string? passwordHash = null)
        => ShareLink.ForVersion(Guid.CreateVersion7(), Guid.CreateVersion7(), label, days, Hash, passwordHash, "user-1", Now);

    [Fact]
    public void ForVersion_vise_la_version_et_expire_apres_la_duree_choisie()
    {
        var link = ForVersion(days: 7, label: "  Studio Delta  ");

        Assert.Equal(ShareTargetType.Version, link.TargetType);
        Assert.NotNull(link.VersionId);
        Assert.Null(link.ReleaseId);
        Assert.Equal("Studio Delta", link.Label);
        Assert.Equal(Now.AddDays(7), link.ExpiresAt);
        Assert.Equal(ShareLinkStatus.Active, link.StatusAt(Now));
        Assert.False(link.HasPassword);
    }

    [Fact]
    public void ForRelease_vise_la_sortie()
    {
        var link = ShareLink.ForRelease(Guid.CreateVersion7(), Guid.CreateVersion7(), null, 30, Hash, "haché", "user-1", Now);

        Assert.Equal(ShareTargetType.Release, link.TargetType);
        Assert.Null(link.VersionId);
        Assert.NotNull(link.ReleaseId);
        Assert.True(link.HasPassword);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    [InlineData(-1)]
    public void Refuse_une_duree_hors_de_1_a_90_jours(int days)
        => Assert.Throws<DomainException>(() => ForVersion(days));

    [Fact]
    public void Refuse_une_empreinte_qui_n_est_pas_un_SHA256()
        => Assert.Throws<ArgumentException>(() =>
            ShareLink.ForVersion(Guid.CreateVersion7(), Guid.CreateVersion7(), null, 30, new byte[10], null, "user-1", Now));

    [Fact]
    public void Expire_a_l_heure_dite()
    {
        var link = ForVersion(days: 1);

        Assert.True(link.IsActiveAt(Now.AddDays(1).AddTicks(-1)));
        Assert.Equal(ShareLinkStatus.Expired, link.StatusAt(Now.AddDays(1)));
    }

    [Fact]
    public void Revoke_coupe_le_lien_et_garde_la_premiere_date()
    {
        var link = ForVersion();

        link.Revoke(Now.AddHours(1));
        link.Revoke(Now.AddHours(2));

        Assert.Equal(Now.AddHours(1), link.RevokedAt);
        Assert.Equal(ShareLinkStatus.Revoked, link.StatusAt(Now.AddHours(3)));
    }

    [Fact]
    public void Chaque_lien_a_son_propre_sel()
        => Assert.NotEqual(ForVersion().AccessSalt, ForVersion().AccessSalt);

    [Fact]
    public void Le_journal_tronque_un_user_agent_trop_long()
    {
        var access = ShareLinkAccess.Create(Guid.CreateVersion7(), ShareAccessKind.Opened, null, null,
            new string('a', 1000), Now);

        Assert.Equal(ShareLinkAccess.UserAgentMaxLength, access.UserAgent!.Length);
    }
}