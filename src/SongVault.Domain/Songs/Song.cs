using SongVault.Domain.Common;

namespace SongVault.Domain.Songs;

public sealed class Song
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

    public static Song Create(string title, string? artist, string? description, DateTimeOffset now)
    {
        var song = new Song { Id = Guid.CreateVersion7(now), CreatedAt = now };
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

    public SongVersion AddVersion(string title, SongVersionStatus status, string? notes, string? lyrics, DateTimeOffset now)
    {
        var number = LastVersionNumber + 1;
        var version = SongVersion.Create(Id, number, title, status, notes, lyrics, now); // valide AVANT d'incrémenter
        LastVersionNumber = number;
        _versions.Add(version);
        UpdatedAt = now;
        return version;
    }

    public SongVersion? FindVersion(Guid versionId) => _versions.SingleOrDefault(v => v.Id == versionId);
}