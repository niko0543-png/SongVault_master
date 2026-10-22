using SongVault.Domain.Common;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Domain.Tests.Files;

/// <summary>Tablature calée sur une maquette (#29) : deux fichiers de la version et des points dans l'ordre de la lecture.</summary>
public sealed class TabSyncTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static (Song Song, SongVersion Version, SongFile Tab, SongFile Demo, SongFile Pdf) VersionWithFiles()
    {
        var song = Song.Create(Guid.NewGuid(), "Nocturne", null, null, Now.AddDays(-1));
        var version = song.AddVersion("Maquette", SongVersionStatus.Demo, null, null, Now.AddDays(-1));
        var tab = version.AddFile("riff.gp5", "k1", "application/octet-stream", 10, SongFileType.Tablature, Now.AddDays(-1));
        var demo = version.AddFile("maquette.wav", "k2", "audio/wav", 10, SongFileType.Audio, Now.AddDays(-1));
        var pdf = version.AddFile("paroles.pdf", "k3", "application/pdf", 5, SongFileType.Document, Now.AddDays(-1));
        return (song, version, tab, demo, pdf);
    }

    private static TabSyncPoint[] Bars(params int[] offsets)
        => [.. offsets.Select((ms, i) => new TabSyncPoint(i, 0, 0, ms))];

    [Fact]
    public void SetTablatureSync_enregistre_la_maquette_et_les_points()
    {
        var (_, version, tab, demo, _) = VersionWithFiles();

        version.SetTablatureSync(tab.Id, demo.Id, Bars(1000, 2500, 4100), Now);

        Assert.Equal(demo.Id, tab.SyncAudioFileId);
        Assert.Equal([1000, 2500, 4100], tab.SyncPoints!.Select(p => p.MillisecondOffset));
        Assert.Equal(Now, version.UpdatedAt);
    }

    [Fact]
    public void SetTablatureSync_remplace_l_ancien_calage()
    {
        var (_, version, tab, demo, _) = VersionWithFiles();
        version.SetTablatureSync(tab.Id, demo.Id, Bars(1000, 2500, 4100), Now);

        version.SetTablatureSync(tab.Id, demo.Id, Bars(800), Now);

        Assert.Equal(800, tab.SyncPoints!.Single().MillisecondOffset);
    }

    [Fact]
    public void Une_reprise_cale_la_meme_mesure_une_seconde_fois()
    {
        var (_, version, tab, demo, _) = VersionWithFiles();

        version.SetTablatureSync(tab.Id, demo.Id, [new(0, 0, 0, 0), new(1, 0, 0, 2000), new(0, 1, 0, 4000), new(1, 1, 0.5, 7000)], Now);

        Assert.Equal(4, tab.SyncPoints!.Count);
    }

    [Fact]
    public void SetTablatureSync_refuse_des_points_qui_reculent_sans_rien_changer()
    {
        var (_, version, tab, demo, _) = VersionWithFiles();
        version.SetTablatureSync(tab.Id, demo.Id, Bars(1000), Now);

        var ex = Assert.Throws<DomainException>(() => version.SetTablatureSync(tab.Id, demo.Id, Bars(1000, 900), Now));

        Assert.Contains("se suivre dans le temps", ex.Message);
        Assert.Equal(1000, tab.SyncPoints!.Single().MillisecondOffset);
    }

    [Fact]
    public void SetTablatureSync_refuse_un_meme_endroit_cale_deux_fois()
    {
        var (_, version, tab, demo, _) = VersionWithFiles();

        Assert.Throws<DomainException>(() => version.SetTablatureSync(tab.Id, demo.Id, [new(2, 0, 0, 1000), new(2, 0, 0, 2000)], Now));
    }

    [Theory]
    [InlineData(-1, 0, 0.0, 0)]
    [InlineData(10_000, 0, 0.0, 0)]
    [InlineData(0, 100, 0.0, 0)]
    [InlineData(0, 0, 1.5, 0)]
    [InlineData(0, 0, double.NaN, 0)]
    [InlineData(0, 0, 0.0, -1)]
    [InlineData(0, 0, 0.0, 21_600_001)]
    public void SetTablatureSync_refuse_un_point_hors_limites(int bar, int occurence, double position, int ms)
    {
        var (_, version, tab, demo, _) = VersionWithFiles();

        Assert.Throws<DomainException>(() => version.SetTablatureSync(tab.Id, demo.Id, [new(bar, occurence, position, ms)], Now));
    }

    [Fact]
    public void SetTablatureSync_refuse_un_calage_vide_ou_trop_long()
    {
        var (_, version, tab, demo, _) = VersionWithFiles();

        Assert.Throws<DomainException>(() => version.SetTablatureSync(tab.Id, demo.Id, [], Now));
        Assert.Throws<DomainException>(() => version.SetTablatureSync(tab.Id, demo.Id,
            [.. Enumerable.Range(0, SongVersion.MaxSyncPoints + 1).Select(i => new TabSyncPoint(i % 5000, i / 5000, 0, i))], Now));
    }

    [Fact]
    public void La_maquette_doit_etre_un_fichier_audio_de_la_version_et_la_tablature_une_tablature()
    {
        var (song, version, tab, demo, pdf) = VersionWithFiles();
        var other = song.AddVersion("Live", SongVersionStatus.Demo, null, null, Now);
        var live = other.AddFile("live.mp3", "k9", "audio/mpeg", 10, SongFileType.Audio, Now);

        Assert.Contains("paroles.pdf", Assert.Throws<DomainException>(() => version.SetTablatureSync(tab.Id, pdf.Id, Bars(0), Now)).Message);
        Assert.Contains("maquette.wav", Assert.Throws<DomainException>(() => version.SetTablatureSync(demo.Id, demo.Id, Bars(0), Now)).Message);
        Assert.Throws<DomainException>(() => version.SetTablatureSync(tab.Id, live.Id, Bars(0), Now));
    }

    [Fact]
    public void Une_maquette_a_la_corbeille_ne_peut_pas_etre_choisie_mais_le_calage_existant_reste()
    {
        var (_, version, tab, demo, _) = VersionWithFiles();
        version.SetTablatureSync(tab.Id, demo.Id, Bars(1000), Now);

        version.TrashFile(demo.Id, "ana", Now);

        Assert.Equal(demo.Id, tab.SyncAudioFileId);                  // il revient si la maquette est restaurée
        Assert.Throws<DomainException>(() => version.SetTablatureSync(tab.Id, demo.Id, Bars(1000), Now));
    }

    [Fact]
    public void ClearTablatureSync_retire_le_calage_et_dit_s_il_y_en_avait_un()
    {
        var (_, version, tab, demo, _) = VersionWithFiles();
        version.SetTablatureSync(tab.Id, demo.Id, Bars(1000), Now);

        Assert.True(version.ClearTablatureSync(tab.Id, Now));
        Assert.Null(tab.SyncAudioFileId);
        Assert.Null(tab.SyncPoints);
        Assert.False(version.ClearTablatureSync(tab.Id, Now));
    }
}