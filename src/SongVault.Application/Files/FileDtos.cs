using SongVault.Application.Abstractions;
using SongVault.Domain.Files;

namespace SongVault.Application.Files;

/// <summary>
/// DTO représentant un fichier de chanson avec ses métadonnées.
/// </summary>
/// <param name="Id">Identifiant unique du fichier</param>
/// <param name="OriginalFileName">Nom du fichier d'origine fourni par l'utilisateur</param>
/// <param name="ContentType">Type MIME du fichier (ex: audio/mpeg)</param>
/// <param name="SizeBytes">Taille du fichier en octets</param>
/// <param name="FileType">Classification du type de fichier (Audio, Video, Document, etc.)</param>
/// <param name="UploadedAt">Date et heure du téléchargement du fichier</param>
public sealed record SongFileDto(Guid Id, string OriginalFileName, string ContentType, long SizeBytes,
    SongFileType FileType, DateTimeOffset UploadedAt)
{
    public static SongFileDto From(SongFile f) =>
        new(f.Id, f.OriginalFileName, f.ContentType, f.SizeBytes, f.FileType, f.UploadedAt);
}

/// <summary>
/// Informations internes nécessaires au téléchargement d'un fichier.
/// La clé de stockage n'est jamais exposée au client.
/// </summary>
/// <param name="StorageKey">Clé unique pour accéder au fichier dans le système de stockage</param>
/// <param name="ContentType">Type MIME du fichier</param>
/// <param name="OriginalFileName">Nom du fichier d'origine</param>
public sealed record StoredFileInfo(string StorageKey, string ContentType, string OriginalFileName);

/// <summary>
/// Un fichier à rendre au client : son contenu (stockage local), ou une URL signée vers laquelle
/// le rediriger (stockage objet, ADR 0012). Un seul des deux est renseigné.
/// </summary>
/// <param name="Content">Flux du contenu du fichier, ou null si RedirectUrl est renseignée</param>
/// <param name="ContentType">Type MIME du fichier</param>
/// <param name="FileName">Nom du fichier à utiliser lors du téléchargement</param>
/// <param name="RedirectUrl">URL signée de lecture, valable peu de temps</param>
public sealed record FileDownload(Stream? Content, string ContentType, string FileName, Uri? RedirectUrl = null);

/// <summary>
/// Commande pour télécharger un fichier de chanson vers une version spécifique.
/// </summary>
/// <param name="SongId">Identifiant de la chanson</param>
/// <param name="VersionId">Identifiant de la version de la chanson</param>
/// <param name="FileName">Nom du fichier à télécharger</param>
/// <param name="Length">Taille du fichier en octets</param>
/// <param name="Content">Flux contenant le contenu du fichier</param>
public sealed record UploadSongFileCommand(Guid SongId, Guid VersionId, string FileName, long Length, Stream Content);


/// <summary>Annonce d'un envoi direct : nom et taille du fichier que le navigateur va envoyer.</summary>
public sealed record StartFileUploadCommand(Guid SongId, Guid VersionId, string FileName, long SizeBytes);

/// <summary>
/// Réponse à une annonce. Direct est l'URL signée du stockage ; null en stockage local,
/// où le navigateur envoie le fichier à l'API (PUT …/uploads/{id}/content).
/// </summary>
public sealed record FileUploadTicket(Guid UploadId, DirectUpload? Direct, DateTimeOffset ExpiresAt);