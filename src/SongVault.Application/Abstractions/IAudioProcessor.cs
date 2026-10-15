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
}

/// <param name="Peaks">AudioPreview.PeakCount points de 0 à 255.</param>
public sealed record AudioAnalysis(byte[] Peaks, int DurationMs);

/// <summary>Fichier illisible. Le message s'affiche tel quel aux membres du groupe ; Detail va dans les journaux.</summary>
public sealed class AudioProcessingException(string message, string? detail = null)
    : Exception(detail is null ? message : $"{message} ({detail})")
{
    public string UserMessage { get; } = message;
}