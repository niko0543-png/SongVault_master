namespace SongVault.Application.Abstractions;

public interface IFileStorageService
{
    /// <summary>Enregistre un fichier neuf ; échoue si la clé existe déjà (jamais d'écrasement silencieux).</summary>
    Task SaveAsync(string storageKey, Stream content, CancellationToken ct);

    /// <exception cref="FileNotFoundException">Aucun fichier sous cette clé.</exception>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);

    /// <summary>Supprime le fichier ; ne fait rien s'il n'existe pas.</summary>
    Task DeleteAsync(string storageKey, CancellationToken ct);

    /// <summary>Taille du fichier en octets, ou null s'il n'existe pas (contrôle d'un envoi direct, ADR 0012).</summary>
    Task<long?> GetLengthAsync(string storageKey, CancellationToken ct);

    /// <summary>
    /// Adresse où le navigateur peut créer ce fichier, et seulement lui, jusqu'à expiresAt.
    /// null si le stockage ne sait pas en produire : l'envoi passe alors par l'API (stockage local).
    /// </summary>
    DirectUpload? CreateUploadUrl(string storageKey, DateTimeOffset expiresAt);

    /// <summary>
    /// Adresse de lecture de ce fichier, valable peu de temps, avec ce type et, si attachmentName est donné,
    /// un téléchargement sous ce nom. null si le stockage ne sait pas en produire : l'API sert alors le fichier.
    /// </summary>
    Uri? CreateDownloadUrl(string storageKey, string contentType, string? attachmentName);
}

/// <summary>Adresse d'envoi direct, et en-têtes HTTP que le navigateur doit joindre à son PUT.</summary>
public sealed record DirectUpload(Uri Url, IReadOnlyDictionary<string, string> Headers);