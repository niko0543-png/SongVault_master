using SongVault.Domain.Common;
using SongVault.Domain.Trash;

namespace SongVault.Domain.Songs;

public sealed class Song : ISoftDeletable
{
    private readonly List<SongVersion> _versions = [];
    public const int TitleMaxLength = 200;
    public const int ArtistMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    public int LastVersionNumber { get; private set; }
    public IReadOnlyCollection<SongVersion> Versions => _versions.AsReadOnly();
    private Song() { } // réservé à EF Core (jeudi) ; le reste du code passe par Create

    public Guid Id { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Artist { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid BandId { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedById { get; private set; }
    public bool IsDeleted => DeletedAt is not null;

    public static Song Create(Guid bandId, string title, string? artist, string? description, DateTimeOffset now)
    {
        if (bandId == Guid.Empty)
            throw new DomainException("Un morceau doit appartenir à un groupe.");

        var song = new Song { Id = Guid.CreateVersion7(now), BandId = bandId, CreatedAt = now };
        song.ApplyDetails(title, artist, description, now);
        return song;
    }

    public void UpdateDetails(string title, string? artist, string? description, DateTimeOffset now)
        => ApplyDetails(title, artist, description, now);

    // Tout est validé AVANT la moindre affectation : un objet n'est jamais laissé à moitié modifié.
    private void ApplyDetails(string title, string? artist, string? description, DateTimeOffset now)
    {
        var cleanTitle = Text.Required(title, TitleMaxLength, "titre");
        var cleanArtist = Text.Optional(artist, ArtistMaxLength, "artiste");
        var cleanDescription = Text.Optional(description, DescriptionMaxLength, "description");

        Title = cleanTitle;
        Artist = cleanArtist;
        Description = cleanDescription;
        UpdatedAt = now;
    }

    public SongVersion AddVersion(string title, SongVersionStatus status, string? notes, string? lyrics,
        DateTimeOffset now, int? bpm = null, MusicalKey? key = null)
    {
        var number = LastVersionNumber + 1;
        var version = SongVersion.Create(Id, number, title, status, notes, lyrics, now, bpm, key);
        LastVersionNumber = number;
        _versions.Add(version);
        UpdatedAt = now;
        return version;
    }

    public SongVersion? FindVersion(Guid versionId) => _versions.SingleOrDefault(v => v.Id == versionId);

    /// <summary>
    /// Met le morceau à la corbeille (#20) avec ses versions et leurs fichiers encore en place, tous avec la même date.
    /// Les versions et fichiers à charger : tous ceux qui sont en place.
    /// </summary>
    public void MoveToTrash(string userId, DateTimeOffset now)
    {
        if (IsDeleted) throw new DomainException("Ce morceau est déjà dans la corbeille.");
        DeletedAt = now;
        DeletedById = userId;
        foreach (var version in _versions.Where(v => !v.IsDeleted))
            version.MoveToTrash(userId, now);
    }

    /// <summary>
    /// Remet le morceau en place avec exactement ce qui est parti avec lui. Une version ou un fichier mis à la
    /// corbeille avant lui y reste. Les versions et fichiers à charger : tous, corbeille comprise.
    /// </summary>
    public void Restore()
    {
        if (!IsDeleted) throw new DomainException("Ce morceau n'est pas dans la corbeille.");
        foreach (var version in _versions.Where(v => v.DeletedAt == DeletedAt))
            version.Restore();
        DeletedAt = null;
        DeletedById = null;
    }

    /// <summary>Met une version et ses fichiers à la corbeille ; null si elle n'est pas en place.</summary>
    public SongVersion? TrashVersion(Guid versionId, string userId, DateTimeOffset now)
    {
        if (IsDeleted) return null;
        var version = _versions.SingleOrDefault(v => v.Id == versionId && !v.IsDeleted);
        version?.MoveToTrash(userId, now);
        return version;
    }

    /// <summary>Remet en place une version mise seule à la corbeille ; null si elle n'y est pas. Le morceau doit être en place.</summary>
    public SongVersion? RestoreVersion(Guid versionId)
    {
        if (IsDeleted) throw new DomainException("Restaurez d'abord le morceau de cette version.");
        var version = _versions.SingleOrDefault(v => v.Id == versionId && v.IsDeleted);
        version?.Restore();
        return version;
    }
}