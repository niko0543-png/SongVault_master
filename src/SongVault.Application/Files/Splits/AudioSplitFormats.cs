using SongVault.Application.Abstractions;

namespace SongVault.Application.Files.Splits;

/// <summary>Le format des passages, déduit de celui de l'original (ADR 0050).</summary>
public static class AudioSplitFormats
{
    /// <summary>Débit des passages réencodés en MP3 (enregistrements WebM, Ogg ou M4A du navigateur).</summary>
    public const int EncodedMp3BitrateKbps = 192;

    public static AudioCutFormat For(string fileNameOrKey) => Path.GetExtension(fileNameOrKey).ToLowerInvariant() switch
    {
        ".wav" => AudioCutFormat.Wav,
        ".mp3" => AudioCutFormat.Mp3,
        ".flac" => AudioCutFormat.Flac,
        _ => AudioCutFormat.Mp3Encoded,
    };

    public static FileTypeRule Rule(AudioCutFormat format)
        => FileTypePolicy.Resolve(format switch
           {
               AudioCutFormat.Wav => "x.wav",
               AudioCutFormat.Flac => "x.flac",
               _ => "x.mp3",
           }) ?? throw new InvalidOperationException($"Format {format} absent de FileTypePolicy.");

    /// <summary>
    /// Ce que les passages occuperont, à peu près, pour vérifier le quota (#16) dès la demande : la part de l'original
    /// qu'ils gardent, ou leur durée au débit du MP3 réencodé. La tâche revérifie avec les tailles réelles.
    /// </summary>
    public static long EstimateBytes(AudioSplitSource source, long keptMs)
    {
        var durationMs = source.DurationMs ?? 0;
        if (durationMs <= 0) return 0;
        return For(source.FileName) == AudioCutFormat.Mp3Encoded
            ? keptMs * EncodedMp3BitrateKbps / 8                            // kbit/s = bit/ms : × ms / 8 = octets
            : (long)((double)source.SizeBytes * keptMs / durationMs);
    }
}
