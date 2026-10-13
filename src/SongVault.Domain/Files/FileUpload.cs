using SongVault.Domain.Common;

namespace SongVault.Domain.Files;

/// <summary>
/// Un envoi direct annoncé, pas encore confirmé (ADR 0012). La ligne disparaît quand le fichier
/// est rattaché à sa version, ou à expiration avec le fichier envoyé.
/// </summary>
public sealed class FileUpload
{
    /// <summary>Validité de l'adresse d'envoi : 2 Go passent en moins d'une heure à 5 Mbit/s.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private FileUpload() { } // réservé à EF Core

    public Guid Id { get; private set; }
    public Guid SongId { get; private set; }
    public Guid SongVersionId { get; private set; }
    public string UserId { get; private set; } = default!;
    public string FileName { get; private set; } = default!;
    public string StorageKey { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public static FileUpload Start(Guid songId, Guid versionId, string userId, string fileName,
        string storageKey, long sizeBytes, DateTimeOffset now)
    {
        if (sizeBytes <= 0) throw new DomainException("Le fichier est vide.");
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("Un envoi appartient à un compte.");
        if (string.IsNullOrWhiteSpace(storageKey)) throw new DomainException("Clé de stockage manquante.");

        return new FileUpload
        {
            Id = Guid.CreateVersion7(now),
            SongId = songId,
            SongVersionId = versionId,
            UserId = userId,
            FileName = Text.Required(fileName, SongFile.OriginalFileNameMaxLength, "nom de fichier"),
            StorageKey = storageKey,
            SizeBytes = sizeBytes,
            CreatedAt = now,
            ExpiresAt = now + Lifetime,
        };
    }

    /// <summary>Seul le compte qui a annoncé l'envoi peut l'envoyer et le confirmer, pour cette version.</summary>
    public bool BelongsTo(Guid songId, Guid versionId, string userId)
        => SongId == songId && SongVersionId == versionId && UserId == userId;

    public bool IsExpiredAt(DateTimeOffset now) => now >= ExpiresAt;
}