using SongVault.Domain.Common;
using SongVault.Domain.Files;
using SongVault.Domain.Trash;

namespace SongVault.Domain.Songs;

public sealed class SongVersion : ISoftDeletable
{
    public const int TitleMaxLength = 200;
    public const int NotesMaxLength = 4000;
    public const int LyricsMaxLength = 20000;
    public const int MinBpm = 20;
    public const int MaxBpm = 300;

    private readonly List<SongFile> _files = [];

    private SongVersion() { }

    public Guid Id { get; private set; }
    public Guid SongId { get; private set; }
    public int Number { get; private set; }
    public string Title { get; private set; } = default!;
    public SongVersionStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public string? Lyrics { get; private set; }
    public int? Bpm { get; private set; }                 // NOUVEAU
    public MusicalKey? Key { get; private set; }          // NOUVEAU
    /// <summary>Code de l'enregistrement (#22) ; une piste de sortie reprend celui de sa version.</summary>
    public Isrc? Isrc { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedById { get; private set; }
    public bool IsDeleted => DeletedAt is not null;
    public IReadOnlyCollection<SongFile> Files => _files.AsReadOnly();

    internal static SongVersion Create(Guid songId, int number, string title, SongVersionStatus status,
        string? notes, string? lyrics, DateTimeOffset now, int? bpm = null, MusicalKey? key = null)
    {
        var version = new SongVersion { Id = Guid.CreateVersion7(now), SongId = songId, Number = number, CreatedAt = now };
        version.Apply(title, status, notes, lyrics, now, bpm, key);
        return version;
    }

    public void Update(string title, SongVersionStatus status, string? notes, string? lyrics,
        DateTimeOffset now, int? bpm = null, MusicalKey? key = null)
        => Apply(title, status, notes, lyrics, now, bpm, key);

    private void Apply(string title, SongVersionStatus status, string? notes, string? lyrics,
        DateTimeOffset now, int? bpm, MusicalKey? key)
    {
        // 1. TOUT valider…
        var cleanTitle = Text.Required(title, TitleMaxLength, "titre");
        var cleanNotes = Text.Optional(notes, NotesMaxLength, "notes");
        var cleanLyrics = Text.Optional(lyrics, LyricsMaxLength, "paroles");
        if (!Enum.IsDefined(status))
            throw new DomainException($"Statut de version inconnu : {status}.");
        if (bpm is < MinBpm or > MaxBpm)
            throw new DomainException($"Le BPM doit être compris entre {MinBpm} et {MaxBpm}.");

        // 2. … PUIS affecter (l'objet n'est jamais laissé à moitié modifié)
        Title = cleanTitle;
        Status = status;
        Notes = cleanNotes;
        Lyrics = cleanLyrics;
        Bpm = bpm;
        Key = key;
        UpdatedAt = now;
    }

    public void SetIsrc(Isrc? isrc, DateTimeOffset now)
    {
        Isrc = isrc;
        UpdatedAt = now;
    }


    public SongFile AddFile(string originalFileName, string storageKey, string contentType,
        long sizeBytes, SongFileType fileType, DateTimeOffset now)
    {
        var file = SongFile.Create(Id, originalFileName, storageKey, contentType, sizeBytes, fileType, now);
        _files.Add(file);
        return file;
    }

    /// <summary>Met un fichier de cette version à la corbeille (#20) ; null s'il n'y est pas, ou plus.</summary>
    public SongFile? TrashFile(Guid fileId, string userId, DateTimeOffset now)
    {
        var file = _files.SingleOrDefault(f => f.Id == fileId && !f.IsDeleted);
        file?.MoveToTrash(userId, now);
        return file;
    }

    /// <summary>Remet en place un fichier mis seul à la corbeille ; null s'il n'y est pas. La version doit être en place.</summary>
    public SongFile? RestoreFile(Guid fileId)
    {
        if (IsDeleted) throw new DomainException("Restaurez d'abord la version de ce fichier.");
        var file = _files.SingleOrDefault(f => f.Id == fileId && f.IsDeleted);
        file?.Restore();
        return file;
    }

    // La version et ses fichiers en place partent avec la même date : la restauration retrouve exactement cet ensemble
    internal void MoveToTrash(string userId, DateTimeOffset now)
    {
        DeletedAt = now;
        DeletedById = userId;
        foreach (var file in _files.Where(f => !f.IsDeleted))
            file.MoveToTrash(userId, now);
    }

    // Un fichier mis à la corbeille avant elle, à une autre date, y reste
    internal void Restore()
    {
        foreach (var file in _files.Where(f => f.DeletedAt == DeletedAt))
            file.Restore();
        DeletedAt = null;
        DeletedById = null;
    }
}