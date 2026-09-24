using SongVault.Domain.Common;

namespace SongVault.Domain.Songs;

public sealed class Song
{
    public const int TitleMaxLength = 200;
    public const int ArtistMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

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
        var cleanTitle = Required(title, TitleMaxLength, "titre");
        var cleanArtist = Optional(artist, ArtistMaxLength, "artiste");
        var cleanDescription = Optional(description, DescriptionMaxLength, "description");

        Title = cleanTitle;
        Artist = cleanArtist;
        Description = cleanDescription;
        UpdatedAt = now;
    }

    private static string Required(string? value, int maxLength, string field)
        => Optional(value, maxLength, field)
           ?? throw new DomainException($"Le champ {field} est obligatoire.");

    private static string? Optional(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > maxLength)
            throw new DomainException($"Le champ {field} ne peut pas dépasser {maxLength} caractères.");
        return trimmed;
    }
}