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
    /// <summary>Pistes du mélangeur (#23) : chaque piste est décodée en entier dans la mémoire du navigateur.</summary>
    public const int MaxStems = 8;
    /// <summary>Points de synchronisation d'une tablature (#29) : un par mesure d'un long morceau, avec de la marge.</summary>
    public const int MaxSyncPoints = 2000;
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

    /// <summary>
    /// Reprend des fichiers d'une autre version du même morceau (ADR 0047) : clé = fichier d'origine en place,
    /// valeur = clé de stockage de sa copie. Les pistes du mélangeur et les calages de tablature suivent
    /// quand tous leurs fichiers sont repris. Les copies sont rendues dans l'ordre de la version d'origine.
    /// </summary>
    public IReadOnlyList<SongFile> CarryOverFiles(SongVersion source, IReadOnlyDictionary<Guid, string> storageKeys,
        DateTimeOffset now)
    {
        // 1. TOUT valider…
        if (source.SongId != SongId || source.Id == Id)
            throw new DomainException("Les fichiers repris viennent d'une autre version de ce morceau.");
        var picked = source._files.Where(f => !f.IsDeleted && storageKeys.ContainsKey(f.Id)).ToList();
        if (picked.Count != storageKeys.Count)
            throw new DomainException("Un fichier repris doit être un fichier en place de la version d'origine.");

        // 2. … PUIS affecter
        var copies = picked.ToDictionary(f => f.Id,
            f => AddFile(f.OriginalFileName, storageKeys[f.Id], f.ContentType, f.SizeBytes, f.FileType, now));

        var stems = picked.Where(f => f.IsStem).OrderBy(f => f.StemPosition).ToList();
        if (!_files.Any(f => !f.IsDeleted && f.IsStem))           // un mélangeur déjà réglé ici n'est pas remplacé
        {
            for (var i = 0; i < stems.Count; i++)
                copies[stems[i].Id].SetStem(stems[i].StemName!, i);
        }
        foreach (var tablature in picked.Where(f => f.SyncAudioFileId is { } audio && copies.ContainsKey(audio)))
            copies[tablature.Id].SetSync(copies[tablature.SyncAudioFileId!.Value].Id, [.. tablature.SyncPoints!]);

        UpdatedAt = now;
        return [.. picked.Select(f => copies[f.Id])];
    }

    /// <summary>Met un fichier de cette version à la corbeille (#20) ; null s'il n'y est pas, ou plus.</summary>
    public SongFile? TrashFile(Guid fileId, string userId, DateTimeOffset now)
    {
        var file = _files.SingleOrDefault(f => f.Id == fileId && !f.IsDeleted);
        file?.ClearStem();                       // #23 : un fichier jeté seul quitte le mélangeur
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

    /// <summary>
    /// Remplace les pistes du mélangeur (#23, ADR 0030) : des fichiers audio en place de cette version, sans doublon,
    /// chacun nommé, dans l'ordre de la liste. Liste vide = plus de mélangeur.
    /// </summary>
    public void SetStems(IReadOnlyList<StemChoice> stems, DateTimeOffset now)
    {
        // 1. TOUT valider…
        if (stems.Count > MaxStems)
            throw new DomainException($"Une version compte au plus {MaxStems} pistes.");
        if (stems.Select(s => s.FileId).Distinct().Count() != stems.Count)
            throw new DomainException("Un même fichier ne peut pas être deux pistes.");
        var chosen = stems.Select(s =>
        {
            var file = _files.SingleOrDefault(f => f.Id == s.FileId && !f.IsDeleted)
                       ?? throw new DomainException("Une piste doit être un fichier de cette version.");
            if (file.FileType != SongFileType.Audio)
                throw new DomainException($"« {file.OriginalFileName} » n'est pas un fichier audio.");
            return (File: file, Name: Text.Required(s.Name, SongFile.StemNameMaxLength, "nom de piste"));
        }).ToList();

        // 2. … PUIS affecter
        foreach (var file in _files.Where(f => !f.IsDeleted))
            file.ClearStem();
        for (var i = 0; i < chosen.Count; i++)
            chosen[i].File.SetStem(chosen[i].Name, i);
        UpdatedAt = now;
    }

    /// <summary>
    /// Cale une tablature de cette version sur une maquette de cette version (#29, ADR 0036) ; remplace l'ancien calage.
    /// Les points sont dans l'ordre de la lecture : leurs instants se suivent, et un même endroit n'est pas calé deux fois.
    /// </summary>
    public void SetTablatureSync(Guid tablatureFileId, Guid audioFileId, IReadOnlyList<TabSyncPoint> points, DateTimeOffset now)
    {
        // 1. TOUT valider…
        var tablature = LiveFile(tablatureFileId, "La tablature doit être un fichier de cette version.");
        if (tablature.FileType != SongFileType.Tablature)
            throw new DomainException($"« {tablature.OriginalFileName} » n'est pas une tablature.");
        var audio = LiveFile(audioFileId, "La maquette doit être un fichier de cette version.");
        if (audio.FileType != SongFileType.Audio)
            throw new DomainException($"« {audio.OriginalFileName} » n'est pas un fichier audio.");
        if (points.Count is 0 or > MaxSyncPoints)
            throw new DomainException($"Un calage compte de 1 à {MaxSyncPoints} points.");
        foreach (var p in points)
        {
            if (p.BarIndex is < 0 or > TabSyncPoint.MaxBarIndex || p.BarOccurence is < 0 or > TabSyncPoint.MaxBarOccurence
                || p.BarPosition is < 0 or > 1 || double.IsNaN(p.BarPosition)
                || p.MillisecondOffset is < 0 or > TabSyncPoint.MaxMillisecondOffset)
                throw new DomainException("Un point de synchronisation sort des limites (mesure, passage, place ou instant).");
        }
        for (var i = 1; i < points.Count; i++)
        {
            if (points[i].MillisecondOffset <= points[i - 1].MillisecondOffset)
                throw new DomainException("Les points de synchronisation doivent se suivre dans le temps.");
        }
        if (points.Select(p => (p.BarIndex, p.BarOccurence, p.BarPosition)).Distinct().Count() != points.Count)
            throw new DomainException("Un même endroit de la tablature ne peut pas être calé deux fois.");

        // 2. … PUIS affecter
        tablature.SetSync(audio.Id, [.. points]);
        UpdatedAt = now;
    }

    /// <summary>Retire le calage d'une tablature (#29) ; false si elle n'était pas calée ou n'est pas dans cette version.</summary>
    public bool ClearTablatureSync(Guid tablatureFileId, DateTimeOffset now)
    {
        var file = _files.SingleOrDefault(f => f.Id == tablatureFileId && !f.IsDeleted);
        if (file?.SyncAudioFileId is null) return false;
        file.ClearSync();
        UpdatedAt = now;
        return true;
    }

    private SongFile LiveFile(Guid fileId, string missing)
        => _files.SingleOrDefault(f => f.Id == fileId && !f.IsDeleted) ?? throw new DomainException(missing);

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