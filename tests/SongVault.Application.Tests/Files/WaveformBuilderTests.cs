using SongVault.Application.Files;
using SongVault.Domain.Files;

namespace SongVault.Application.Tests.Files;

public sealed class WaveformBuilderTests
{
    /// <summary>PCM 16 bits little-endian, comme sur la sortie de ffmpeg.</summary>
    private static byte[] Pcm(params short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++) BitConverter.TryWriteBytes(bytes.AsSpan(i * 2), samples[i]);
        return bytes;
    }

    private static short[] Repeat(short value, int count) => Enumerable.Repeat(value, count).ToArray();

    [Fact]
    public void La_duree_vient_du_nombre_d_echantillons()
    {
        var builder = new WaveformBuilder();

        builder.Add(Pcm(Repeat(1000, WaveformBuilder.SampleRate * 3 / 2)));     // 1,5 s

        Assert.Equal(1500, builder.DurationMs);
        Assert.Equal(AudioPreview.PeakCount, builder.Build().Length);
    }

    [Fact]
    public void Le_plus_fort_vaut_255_et_un_passage_moitie_moins_fort_128()
    {
        var builder = new WaveformBuilder();
        builder.Add(Pcm(Repeat(-16000, WaveformBuilder.SampleRate)));          // 1 s à mi-volume (le signe ne compte pas)
        builder.Add(Pcm(Repeat(32000, WaveformBuilder.SampleRate)));           // 1 s fort

        var peaks = builder.Build();

        Assert.Equal(128, peaks[0]);
        Assert.Equal(255, peaks[^1]);
        Assert.All(peaks[..500], p => Assert.Equal(128, p));
    }

    [Fact]
    public void Un_echantillon_coupe_entre_deux_lectures_n_est_pas_perdu()
    {
        var whole = new WaveformBuilder();
        var split = new WaveformBuilder();
        var pcm = Pcm([.. Repeat(100, 799), 30000, .. Repeat(100, 800)]);

        whole.Add(pcm);
        split.Add(pcm.AsSpan(0, 1599));            // coupe le 800e échantillon, le plus fort, en son milieu
        split.Add(pcm.AsSpan(1599));

        Assert.Equal(whole.DurationMs, split.DurationMs);
        Assert.Equal(whole.Build(), split.Build());
    }

    [Fact]
    public void Un_son_tres_court_est_etire_sur_toute_la_largeur()
    {
        var builder = new WaveformBuilder();
        builder.Add(Pcm(Repeat(20000, 40)));                                    // 5 ms : moins d'un bloc

        var peaks = builder.Build();

        Assert.Equal(5, builder.DurationMs);
        Assert.All(peaks, p => Assert.Equal(255, p));
    }

    [Fact]
    public void Un_silence_donne_une_ligne_plate()
    {
        var builder = new WaveformBuilder();
        builder.Add(Pcm(Repeat(0, WaveformBuilder.SampleRate)));

        Assert.All(builder.Build(), p => Assert.Equal(0, p));
        Assert.Equal(1000, builder.DurationMs);
    }
}