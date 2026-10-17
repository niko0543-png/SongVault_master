using SongVault.Domain.Files;

namespace SongVault.Application.Abstractions;

/// <summary>Analyses antivirus des fichiers (ADR 0024). Sans filtre de groupe : la tâche de fond n'a pas de requête HTTP.</summary>
public interface IFileScanRepository
{
    void Add(FileScan scan);
    Task<FileScan?> GetAsync(Guid songFileId, CancellationToken ct);
    /// <summary>Jusqu'à <paramref name="take"/> analyses NotScanned, suivies, les plus anciennes d'abord.</summary>
    Task<IReadOnlyList<FileScan>> ListNotScannedAsync(int take, CancellationToken ct);
    /// <summary>Ce que l'analyse lit : la clé de l'original et son type ; null si le fichier a été supprimé.</summary>
    Task<FileScanTarget?> GetTargetAsync(Guid songFileId, CancellationToken ct);
    /// <summary>
    /// De quoi prévenir le compte qui a envoyé un fichier infecté (événement FileAdded du fil, #11) ;
    /// null si le fichier a été supprimé, n'est pas infecté, ou si ce compte n'existe plus.
    /// </summary>
    Task<InfectedFileNotice?> GetInfectedNoticeAsync(Guid songFileId, CancellationToken ct);
}

public sealed record FileScanTarget(string StorageKey, SongFileType FileType);

public sealed record InfectedFileNotice(
    string UploaderEmail, Guid BandId, string BandName, Guid SongId, string SongTitle, Guid VersionId, int VersionNumber,
    string FileName, string Threat);