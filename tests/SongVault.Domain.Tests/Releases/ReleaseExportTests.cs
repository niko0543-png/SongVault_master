using SongVault.Domain.Common;
using SongVault.Domain.Releases;

namespace SongVault.Domain.Tests.Releases;

public sealed class ReleaseExportTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.NewGuid();
    private static readonly Guid ReleaseId = Guid.NewGuid();

    [Fact]
    public void Prete_elle_se_telecharge_pendant_24_heures()
    {
        var export = ReleaseExport.Request(BandId, ReleaseId, "u1", Now);

        export.MarkReady(1234, Now);

        Assert.Equal(ReleaseExportStatus.Ready, export.Status);
        Assert.Equal(Now.AddHours(24), export.ExpiresAt);
        Assert.True(export.IsDownloadableAt(Now.AddHours(24).AddTicks(-1)));
        Assert.False(export.IsDownloadableAt(Now.AddHours(24)));
        Assert.True(export.IsExpiredAt(Now.AddHours(24)));
    }

    [Fact]
    public void En_preparation_elle_ne_se_telecharge_pas_et_n_est_pas_expiree()
    {
        var export = ReleaseExport.Request(BandId, ReleaseId, "u1", Now);

        Assert.False(export.IsDownloadableAt(Now));
        Assert.False(export.IsExpiredAt(Now.AddDays(3)));
    }

    [Fact]
    public void La_cle_de_stockage_se_deduit_de_l_identifiant()
    {
        var export = ReleaseExport.Request(BandId, ReleaseId, "u1", Now);

        Assert.Equal($"release-export-{export.Id:N}.zip", export.StorageKey);
        Assert.Equal(export.StorageKey, ReleaseExport.StorageKeyFor(export.Id));
    }

    [Fact]
    public void Un_echec_ne_change_pas_une_archive_deja_prete()
    {
        var export = ReleaseExport.Request(BandId, ReleaseId, "u1", Now);
        export.MarkReady(1234, Now);

        export.MarkFailed(Now);

        Assert.Equal(ReleaseExportStatus.Ready, export.Status);
        Assert.Throws<DomainException>(() => export.MarkReady(1, Now));
    }

    [Fact]
    public void Une_archive_echouee_ne_devient_pas_prete()
    {
        var export = ReleaseExport.Request(BandId, ReleaseId, "u1", Now);
        export.MarkFailed(Now);

        Assert.Equal(ReleaseExportStatus.Failed, export.Status);
        Assert.Equal(Now, export.CompletedAt);
        Assert.Throws<DomainException>(() => export.MarkReady(1234, Now));
    }

    [Theory]
    [InlineData(false, true, "u1")]
    [InlineData(true, false, "u1")]
    [InlineData(true, true, " ")]
    public void Une_demande_incomplete_est_refusee(bool withBand, bool withRelease, string user)
        => Assert.Throws<DomainException>(() => ReleaseExport.Request(
            withBand ? BandId : Guid.Empty, withRelease ? ReleaseId : Guid.Empty, user, Now));

    [Fact]
    public void Une_taille_nulle_est_refusee()
        => Assert.Throws<DomainException>(() => ReleaseExport.Request(BandId, ReleaseId, "u1", Now).MarkReady(0, Now));
}