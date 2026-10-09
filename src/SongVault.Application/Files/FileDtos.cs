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
/// Représente un fichier téléchargé par le client avec son contenu et métadonnées.
/// </summary>
/// <param name="Content">Flux du contenu du fichier</param>
/// <param name="ContentType">Type MIME du fichier</param>
/// <param name="FileName">Nom du fichier à utiliser lors du téléchargement</param>
public sealed record FileDownload(Stream Content, string ContentType, string FileName);

/// <summary>
/// Commande pour télécharger un fichier de chanson vers une version spécifique.
/// </summary>
/// <param name="SongId">Identifiant de la chanson</param>
/// <param name="VersionId">Identifiant de la version de la chanson</param>
/// <param name="FileName">Nom du fichier à télécharger</param>
/// <param name="Length">Taille du fichier en octets</param>
/// <param name="Content">Flux contenant le contenu du fichier</param>
public sealed record UploadSongFileCommand(Guid SongId, Guid VersionId, string FileName, long Length, Stream Content);