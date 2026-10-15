using SongVault.Domain.Common;

namespace SongVault.Domain.Files;

public enum AudioPreviewStatus { Pending, Ready, Failed }

/// <summary>
/// Aperçu d'un fichier audio (ADR 0021) : sa forme d'onde, sa durée et, pour un WAV ou un FLAC, un MP3 léger pour l'écoute.
/// Préparé en tâche de fond après l'envoi ; l'original n'est jamais modifié.
/// </summary>
public sealed class AudioPreview
{
    /// <summary>Nombre de points de la forme d'onde, chacun de 0 à 255 (le plus fort du fichier vaut 255).</summary>
    public const int PeakCount = 1000;
    public const int ErrorMaxLength = 300;

    private AudioPreview() { } // réservé à EF Core

    public Guid SongFileId { get; private set; }
    public AudioPreviewStatus Status { get; private set; }
    /// <summary>MP3 d'écoute ; null si l'original est déjà un MP3 (on écoute alors l'original).</summary>
    public string? PreviewStorageKey { get; private set; }
    public byte[]? Peaks { get; private set; }
    public int? DurationMs { get; private set; }
    /// <summary>Raison lisible du dernier échec, montrée aux membres du groupe.</summary>
    public string? Error { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Clé du MP3 d'écoute : toujours la même pour un fichier, donc jamais deux MP3 pour un même original.</summary>
    public static string StorageKeyFor(Guid songFileId) => $"preview-{songFileId:N}.mp3";

    public static AudioPreview Request(Guid songFileId, DateTimeOffset now)
    {
        if (songFileId == Guid.Empty) throw new DomainException("Un aperçu appartient à un fichier.");
        return new AudioPreview { SongFileId = songFileId, Status = AudioPreviewStatus.Pending, RequestedAt = now };
    }

    public void MarkReady(string? previewStorageKey, byte[] peaks, int durationMs, DateTimeOffset now)
    {
        if (Status != AudioPreviewStatus.Pending) throw new DomainException("Cet aperçu n'est plus en préparation.");
        if (peaks.Length != PeakCount) throw new DomainException($"La forme d'onde doit compter {PeakCount} points.");
        if (durationMs <= 0) throw new DomainException("La durée doit être positive.");

        (Status, PreviewStorageKey, Peaks, DurationMs, Error, CompletedAt) =
            (AudioPreviewStatus.Ready, previewStorageKey, peaks, durationMs, null, now);
    }

    /// <summary>Dernier essai manqué. Sans effet si l'aperçu n'est plus en préparation.</summary>
    public void MarkFailed(string error, DateTimeOffset now)
    {
        if (Status != AudioPreviewStatus.Pending) return;
        var reason = string.IsNullOrWhiteSpace(error) ? "Conversion impossible." : error.Trim();
        (Status, Error, CompletedAt) =
            (AudioPreviewStatus.Failed, reason.Length > ErrorMaxLength ? reason[..ErrorMaxLength] : reason, now);
    }

    /// <summary>Relance après un échec, à la demande d'un membre.</summary>
    public void Retry(DateTimeOffset now)
    {
        if (Status != AudioPreviewStatus.Failed) throw new DomainException("Seul un aperçu en échec peut être relancé.");
        (Status, Error, CompletedAt, RequestedAt) = (AudioPreviewStatus.Pending, null, null, now);
    }
}