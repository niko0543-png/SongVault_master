using SongVault.Domain.Common;

namespace SongVault.Domain.Files;

public sealed class SongFile
{
    public const int OriginalFileNameMaxLength = 255;

    private SongFile() { }

    public Guid Id { get; private set; }
    public Guid SongVersionId { get; private set; }
    public string OriginalFileName { get; private set; } = default!;
    public string StorageKey { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public SongFileType FileType { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }

    internal static SongFile Create(Guid versionId, string originalFileName, string storageKey,
        string contentType, long sizeBytes, SongFileType fileType, DateTimeOffset now)
    {
        if (sizeBytes <= 0) throw new DomainException("Le fichier est vide.");
        if (string.IsNullOrWhiteSpace(storageKey)) throw new DomainException("Clé de stockage manquante.");

        return new SongFile
        {
            Id = Guid.CreateVersion7(now),
            SongVersionId = versionId,
            OriginalFileName = Text.Required(originalFileName, OriginalFileNameMaxLength, "nom de fichier"),
            StorageKey = storageKey,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            FileType = fileType,
            UploadedAt = now,
        };
    }
}