using SongVault.Domain.Common;
using SongVault.Domain.Trash;

namespace SongVault.Domain.Files;

public sealed class SongFile : ISoftDeletable
{
    public const int OriginalFileNameMaxLength = 255;
    public const int StemNameMaxLength = 60;

    private SongFile() { }

    public Guid Id { get; private set; }
    public Guid SongVersionId { get; private set; }
    public string OriginalFileName { get; private set; } = default!;
    public string StorageKey { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public SongFileType FileType { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedById { get; private set; }
    public bool IsDeleted => DeletedAt is not null;
    /// <summary>Nom de la piste dans le mélangeur (#23, ADR 0030) ; null si le fichier n'est pas une piste.</summary>
    public string? StemName { get; private set; }
    /// <summary>Place de la piste dans le mélangeur, à partir de 0 ; null si le fichier n'est pas une piste.</summary>
    public int? StemPosition { get; private set; }
    public bool IsStem => StemPosition is not null;
    /// <summary>Tablature calée sur une maquette (#29, ADR 0036) : le fichier audio de la même version ; null sinon.</summary>
    public Guid? SyncAudioFileId { get; private set; }
    /// <summary>Les points de synchronisation, dans l'ordre de la lecture ; null si la tablature n'est pas calée.</summary>
    public IReadOnlyList<TabSyncPoint>? SyncPoints { get; private set; }

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

    internal void SetStem(string name, int position)
    {
        StemName = name;
        StemPosition = position;
    }

    internal void ClearStem()
    {
        StemName = null;
        StemPosition = null;
    }

    internal void SetSync(Guid audioFileId, IReadOnlyList<TabSyncPoint> points)
    {
        SyncAudioFileId = audioFileId;
        SyncPoints = points;
    }

    internal void ClearSync()
    {
        SyncAudioFileId = null;
        SyncPoints = null;
    }

    // #20 : la corbeille passe par la version ou le morceau, qui marquent tout avec la même date
    internal void MoveToTrash(string userId, DateTimeOffset now)
    {
        DeletedAt = now;
        DeletedById = userId;
    }

    internal void Restore()
    {
        DeletedAt = null;
        DeletedById = null;
    }
}