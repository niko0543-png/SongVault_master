using SongVault.Application.Files;
using SongVault.Domain.Files;

namespace SongVault.Api.Contracts.Files;

/// <param name="Preview">Fichier audio : Pending (en préparation), Ready ou Failed (#15). null pour les autres fichiers.</param>
/// <param name="Scan">
/// Analyse antivirus (#17) : Pending (en cours), Clean (sain), Infected (bloqué) ou Failed (à relancer).
/// Seul un fichier Clean s'écoute, se télécharge et se partage.
/// </param>
/// <param name="ScanThreat">Fichier Infected : nom de la menace trouvée par l'antivirus.</param>
public sealed record SongFileResponse(Guid Id, string OriginalFileName, string ContentType, long SizeBytes,
    SongFileType FileType, DateTimeOffset UploadedAt, AudioPreviewStatus? Preview, FileScanStatus Scan, string? ScanThreat)
{
    public static SongFileResponse From(SongFileDto d) =>
        new(d.Id, d.OriginalFileName, d.ContentType, d.SizeBytes, d.FileType, d.UploadedAt, d.Preview, d.Scan, d.ScanThreat);
}

/// <summary>Forme d'onde d'un fichier audio (#15).</summary>
/// <param name="DurationMs">Durée du son, en millisecondes.</param>
/// <param name="Peaks">1000 points de 0 à 255, du début à la fin ; le plus fort du fichier vaut 255.</param>
public sealed record WaveformResponse(int DurationMs, IReadOnlyList<int> Peaks)
{
    public static WaveformResponse From(AudioWaveformDto d) => new(d.DurationMs, d.Peaks);
}

/// <summary>État de l'aperçu après une demande de relance (#15).</summary>
public sealed record AudioPreviewResponse(AudioPreviewStatus Status);

/// <summary>État de l'analyse antivirus après une demande de relance (#17).</summary>
public sealed record FileScanResponse(FileScanStatus Status);