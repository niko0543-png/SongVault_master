using SongVault.Domain.Common;
using SongVault.Domain.Files;

namespace SongVault.Domain.Tests.Files;

public sealed class AudioPreviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Peaks = new byte[AudioPreview.PeakCount];

    [Fact]
    public void Pret_il_garde_la_forme_d_onde_la_duree_et_le_mp3()
    {
        var preview = AudioPreview.Request(Guid.NewGuid(), Now);

        preview.MarkReady("preview-x.mp3", Peaks, 183_000, Now.AddMinutes(1));

        Assert.Equal(AudioPreviewStatus.Ready, preview.Status);
        Assert.Equal("preview-x.mp3", preview.PreviewStorageKey);
        Assert.Equal(183_000, preview.DurationMs);
        Assert.Equal(Now.AddMinutes(1), preview.CompletedAt);
    }

    [Fact]
    public void Refuse_une_forme_d_onde_d_une_autre_taille()
    {
        var preview = AudioPreview.Request(Guid.NewGuid(), Now);

        Assert.Throws<DomainException>(() => preview.MarkReady(null, new byte[10], 1000, Now));
    }

    [Fact]
    public void En_echec_il_se_relance_et_oublie_l_erreur()
    {
        var preview = AudioPreview.Request(Guid.NewGuid(), Now);
        preview.MarkFailed("Fichier abîmé.", Now);
        Assert.Equal(AudioPreviewStatus.Failed, preview.Status);
        Assert.Equal("Fichier abîmé.", preview.Error);

        preview.Retry(Now.AddHours(1));

        Assert.Equal(AudioPreviewStatus.Pending, preview.Status);
        Assert.Null(preview.Error);
        Assert.Equal(Now.AddHours(1), preview.RequestedAt);
    }

    [Fact]
    public void Ne_se_relance_pas_s_il_est_pret_ou_en_preparation()
    {
        var pending = AudioPreview.Request(Guid.NewGuid(), Now);
        var ready = AudioPreview.Request(Guid.NewGuid(), Now);
        ready.MarkReady(null, Peaks, 1000, Now);

        Assert.Throws<DomainException>(() => pending.Retry(Now));
        Assert.Throws<DomainException>(() => ready.Retry(Now));
    }

    [Fact]
    public void Un_echec_ne_change_pas_un_apercu_deja_pret()
    {
        var preview = AudioPreview.Request(Guid.NewGuid(), Now);
        preview.MarkReady(null, Peaks, 1000, Now);

        preview.MarkFailed("trop tard", Now);

        Assert.Equal(AudioPreviewStatus.Ready, preview.Status);
        Assert.Null(preview.Error);
    }

    [Fact]
    public void La_cle_du_mp3_ne_depend_que_du_fichier()
        => Assert.Equal("preview-0192dddd000070008000000000000001.mp3",
            AudioPreview.StorageKeyFor(Guid.Parse("0192dddd-0000-7000-8000-000000000001")));
}