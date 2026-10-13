namespace SongVault.Api.Contracts.Files;

/// <param name="FileName">Nom du fichier, avec son extension.</param>
/// <param name="SizeBytes">Taille exacte en octets : elle est revérifiée à la confirmation.</param>
public sealed record StartFileUploadRequest(string FileName, long SizeBytes);

/// <param name="UploadId">À renvoyer à la confirmation.</param>
/// <param name="UploadUrl">Adresse du PUT : URL signée du stockage, ou route de l'API en stockage local.</param>
/// <param name="Headers">En-têtes HTTP à joindre au PUT.</param>
/// <param name="ExpiresAt">Après cette date, l'envoi est effacé et doit être recommencé.</param>
public sealed record FileUploadResponse(
    Guid UploadId, string UploadUrl, IReadOnlyDictionary<string, string> Headers, DateTimeOffset ExpiresAt);