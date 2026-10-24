namespace SongVault.Application.Abstractions;

/// <summary>Lit un fichier audio de bout en bout (ADR 0021). Implémenté avec ffmpeg dans l'infrastructure.</summary>
public interface IAudioProcessor
{
    /// <summary>
    /// Calcule la forme d'onde et la durée de <paramref name="source"/>, lu une seule fois du début à la fin.
    /// Si <paramref name="mp3OutputPath"/> est donné, y écrit en même temps la version MP3 d'écoute.
    /// </summary>
    /// <exception cref="AudioProcessingException">Le fichier n'a pas pu être lu comme de l'audio.</exception>
    Task<AudioAnalysis> ProcessAsync(Stream source, string? mp3OutputPath, CancellationToken ct);

    /// <summary>
    /// Découpe <paramref name="source"/>, lu une seule fois du début à la fin, en un fichier par passage (ADR 0050).
    /// </summary>
    /// <exception cref="AudioProcessingException">Le fichier n'a pas pu être lu comme de l'audio.</exception>
    Task SplitAsync(Stream source, IReadOnlyList<AudioCut> cuts, AudioCutFormat format, CancellationToken ct);
}

/// <summary>Un passage à écrire dans <paramref name="OutputPath"/>.</summary>
public sealed record AudioCut(int StartMs, int DurationMs, string OutputPath);

/// <summary>
/// Format des passages (ADR 0050) : WAV et MP3 recopiés sans réencodage, FLAC réencodé sans perte (sa durée
/// d'en-tête serait fausse sinon), tout le reste (WebM, Ogg, M4A d'un enregistrement du navigateur) en MP3.
/// </summary>
public enum AudioCutFormat { Wav, Mp3, Flac, Mp3Encoded }

/// <param name="Peaks">AudioPreview.PeakCount points de 0 à 255.</param>
public sealed record AudioAnalysis(byte[] Peaks, int DurationMs);

/// <summary>Fichier illisible. Le message s'affiche tel quel aux membres du groupe ; Detail va dans les journaux.</summary>
public sealed class AudioProcessingException(string message, string? detail = null)
    : Exception(detail is null ? message : $"{message} ({detail})")
{
    public string UserMessage { get; } = message;
}