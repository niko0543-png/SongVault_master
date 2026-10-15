using SongVault.Domain.Files;

namespace SongVault.Application.Files;

/// <summary>
/// Forme d'onde d'un son reçu au fil de l'eau (ADR 0021) : PCM 16 bits signé, mono, à <see cref="SampleRate"/> Hz.
/// La durée n'est connue qu'à la fin : on garde le pic de chaque bloc de 10 ms, puis on les regroupe en
/// <see cref="AudioPreview.PeakCount"/> points. Trois heures de son tiennent en un million de blocs (2 Mo).
/// </summary>
public sealed class WaveformBuilder
{
    public const int SampleRate = 8000;
    private const int SamplesPerBlock = SampleRate / 100;

    private readonly List<ushort> _blocks = [];
    private long _samples;
    private int _blockPeak;
    private int _inBlock;
    private int? _pendingLowByte;            // un échantillon coupé en deux entre deux lectures

    public void Add(ReadOnlySpan<byte> pcm)
    {
        var i = 0;
        if (_pendingLowByte is { } low && pcm.Length > 0)
        {
            AddSample((short)(low | (pcm[0] << 8)));
            _pendingLowByte = null;
            i = 1;
        }
        for (; i + 1 < pcm.Length; i += 2) AddSample((short)(pcm[i] | (pcm[i + 1] << 8)));
        if (i < pcm.Length) _pendingLowByte = pcm[i];
    }

    public int DurationMs => (int)Math.Min(int.MaxValue, _samples * 1000 / SampleRate);

    /// <summary>Les points de la forme d'onde, le plus fort ramené à 255 ; tous à 0 pour un silence.</summary>
    public byte[] Build()
    {
        var blocks = _inBlock > 0 ? [.. _blocks, (ushort)_blockPeak] : _blocks.ToArray();
        var peaks = new byte[AudioPreview.PeakCount];
        if (blocks.Length == 0) return peaks;

        var points = new int[AudioPreview.PeakCount];
        for (var p = 0; p < points.Length; p++)
        {
            // Chaque point couvre une part égale des blocs (au moins un : un son court est étiré)
            var from = (int)((long)p * blocks.Length / points.Length);
            var to = Math.Max(from + 1, (int)((long)(p + 1) * blocks.Length / points.Length));
            for (var b = from; b < to && b < blocks.Length; b++) points[p] = Math.Max(points[p], blocks[b]);
        }

        var loudest = points.Max();
        if (loudest == 0) return peaks;
        for (var p = 0; p < peaks.Length; p++) peaks[p] = (byte)Math.Round(points[p] * 255.0 / loudest);
        return peaks;
    }

    private void AddSample(short sample)
    {
        _samples++;
        _blockPeak = Math.Max(_blockPeak, sample == short.MinValue ? short.MaxValue : Math.Abs(sample));
        if (++_inBlock < SamplesPerBlock) return;
        _blocks.Add((ushort)_blockPeak);
        (_blockPeak, _inBlock) = (0, 0);
    }
}