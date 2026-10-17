using SongVault.Domain.Files;

namespace SongVault.Application.Abstractions;

/// <summary>Aperçus des fichiers audio (ADR 0021). Sans filtre de groupe : la tâche de fond n'a pas de requête HTTP.</summary>
public interface IAudioPreviewRepository
{
    void Add(AudioPreview preview);
    Task<AudioPreview?> GetAsync(Guid songFileId, CancellationToken ct);
    /// <summary>Clé de stockage de l'original, ou null si le fichier a été supprimé ou n'est pas déclaré sain (#17).</summary>
    Task<string?> GetSourceKeyAsync(Guid songFileId, CancellationToken ct);
}