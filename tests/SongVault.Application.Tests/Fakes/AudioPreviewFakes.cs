using SongVault.Application.Abstractions;
using SongVault.Domain.Files;

namespace SongVault.Application.Tests.Fakes;

/// <summary>Aperçus en mémoire ; Sources : clé de stockage de l'original de chaque fichier.</summary>
internal sealed class FakeAudioPreviewRepository : IAudioPreviewRepository
{
    public List<AudioPreview> Previews { get; } = [];
    public Dictionary<Guid, string> Sources { get; } = [];

    public void Add(AudioPreview preview) => Previews.Add(preview);

    public Task<AudioPreview?> GetAsync(Guid songFileId, CancellationToken ct)
        => Task.FromResult(Previews.SingleOrDefault(p => p.SongFileId == songFileId));

    public Task<string?> GetSourceKeyAsync(Guid songFileId, CancellationToken ct)
        => Task.FromResult(Sources.GetValueOrDefault(songFileId));
}

/// <summary>ffmpeg factice : lit la source, écrit "MP3" si on lui demande un MP3, ou lève Failure.</summary>
internal sealed class FakeAudioProcessor : IAudioProcessor
{
    public Exception? Failure { get; set; }
    public int Calls { get; private set; }

    public async Task<AudioAnalysis> ProcessAsync(Stream source, string? mp3OutputPath, CancellationToken ct)
    {
        Calls++;
        await source.CopyToAsync(Stream.Null, ct);
        if (Failure is not null) throw Failure;
        if (mp3OutputPath is not null) await File.WriteAllBytesAsync(mp3OutputPath, "MP3"u8.ToArray(), ct);
        return new AudioAnalysis(new byte[AudioPreview.PeakCount], 183_000);
    }
}