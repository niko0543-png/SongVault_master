using SongVault.Domain.Files;

namespace SongVault.Application.Abstractions;

/// <summary>Accès aux envois directs en attente. Un seul dépôt pour l'agrégat FileUpload (ADR 0006).</summary>
public interface IFileUploadRepository
{
    /// <summary>Ajoute un envoi au contexte. N'écrit rien : l'appelant fait SaveChangesAsync.</summary>
    void Add(FileUpload upload);

    /// <summary>Envoi suivi par EF, ou null.</summary>
    Task<FileUpload?> GetAsync(Guid uploadId, CancellationToken ct);

    /// <summary>Retire l'envoi du contexte ; effacé au prochain SaveChangesAsync.</summary>
    void Remove(FileUpload upload);
}