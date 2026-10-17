using SongVault.Domain.Common;
using SongVault.Domain.Files;

namespace SongVault.Domain.Tests.Files;

public sealed class FileScanTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Une_analyse_commence_en_attente_et_le_fichier_n_est_pas_lisible()
    {
        var scan = FileScan.Request(Guid.NewGuid(), Now);

        Assert.Equal(FileScanStatus.Pending, scan.Status);
        Assert.False(scan.IsReadable);
    }

    [Fact]
    public void Sain_le_fichier_devient_lisible()
    {
        var scan = FileScan.Request(Guid.NewGuid(), Now);

        scan.MarkClean(Now.AddSeconds(5));

        Assert.True(scan.IsReadable);
        Assert.Equal(Now.AddSeconds(5), scan.CompletedAt);
    }

    [Fact]
    public void Infecte_il_garde_le_nom_de_la_menace_et_reste_illisible()
    {
        var scan = FileScan.Request(Guid.NewGuid(), Now);

        scan.MarkInfected("  Win.Test.EICAR_HDB-1 ", Now);

        Assert.Equal(FileScanStatus.Infected, scan.Status);
        Assert.Equal("Win.Test.EICAR_HDB-1", scan.Threat);
        Assert.False(scan.IsReadable);
        Assert.Throws<DomainException>(() => scan.MarkClean(Now));       // le verdict ne change plus
        Assert.Throws<DomainException>(() => scan.Retry(Now));
    }

    [Fact]
    public void Un_nom_de_menace_trop_long_est_coupe()
    {
        var scan = FileScan.Request(Guid.NewGuid(), Now);

        scan.MarkInfected(new string('x', 500), Now);

        Assert.Equal(FileScan.ThreatMaxLength, scan.Threat!.Length);
    }

    [Fact]
    public void En_echec_elle_se_relance_et_oublie_l_erreur()
    {
        var scan = FileScan.Request(Guid.NewGuid(), Now);
        scan.MarkFailed("Antivirus injoignable.", Now);
        Assert.Equal(FileScanStatus.Failed, scan.Status);
        Assert.Equal("Antivirus injoignable.", scan.Error);

        scan.Retry(Now.AddHours(1));

        Assert.Equal(FileScanStatus.Pending, scan.Status);
        Assert.Null(scan.Error);
        Assert.Equal(Now.AddHours(1), scan.RequestedAt);
    }

    [Fact]
    public void Un_echec_tardif_ne_change_pas_un_verdict_deja_rendu()
    {
        var scan = FileScan.Request(Guid.NewGuid(), Now);
        scan.MarkClean(Now);

        scan.MarkFailed("Trop tard.", Now);

        Assert.Equal(FileScanStatus.Clean, scan.Status);
    }

    [Fact]
    public void Refuse_un_fichier_vide()
        => Assert.Throws<DomainException>(() => FileScan.Request(Guid.Empty, Now));

    [Fact]
    public void Antivirus_desactive_le_fichier_est_lisible_sans_verdict()
    {
        var scan = FileScan.Request(Guid.NewGuid(), Now);

        scan.MarkNotScanned(Now.AddSeconds(2));

        Assert.Equal(FileScanStatus.NotScanned, scan.Status);
        Assert.True(scan.IsReadable);
        Assert.Throws<DomainException>(() => scan.Retry(Now));          // rien n'a échoué
    }

    [Fact]
    public void A_l_activation_un_fichier_non_analyse_repasse_en_attente()
    {
        var scan = FileScan.Request(Guid.NewGuid(), Now);
        scan.MarkNotScanned(Now);

        scan.RequestAfterActivation(Now.AddDays(30));

        Assert.Equal(FileScanStatus.Pending, scan.Status);
        Assert.False(scan.IsReadable);
        Assert.Null(scan.CompletedAt);
        Assert.Equal(Now.AddDays(30), scan.RequestedAt);
    }

    [Fact]
    public void Seul_un_fichier_non_analyse_est_repris_a_l_activation()
    {
        var scan = FileScan.Request(Guid.NewGuid(), Now);
        scan.MarkClean(Now);

        Assert.Throws<DomainException>(() => scan.RequestAfterActivation(Now));
    }
}