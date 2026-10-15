namespace SongVault.Infrastructure.Audio;

public sealed class AudioPreviewOptions
{
    public const string SectionName = "AudioPreview";

    /// <summary>Chemin de ffmpeg ; par défaut, celui trouvé dans le PATH (installé dans l'image Docker).</summary>
    public string FfmpegPath { get; set; } = "ffmpeg";
    /// <summary>Au-delà, la conversion est arrêtée et comptée comme un essai manqué. Moins que BackgroundJobs:StaleAfter.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(20);
    /// <summary>Débit du MP3 d'écoute.</summary>
    public int Mp3BitrateKbps { get; set; } = 128;
}