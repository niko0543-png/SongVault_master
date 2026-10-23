using SongVault.Domain.Common;

namespace SongVault.Domain.Setlists;

/// <summary>
/// Une setlist du groupe (concert, répétition) et ses morceaux dans l'ordre de jeu (ADR 0028).
/// Même mécanique de liste ordonnée que les pistes d'une sortie (ADR 0016).
/// </summary>
public sealed class Setlist
{
    public const int NameMaxLength = 200;
    public const int VenueMaxLength = 200;
    /// <summary>Au-delà, la feuille imprimée ne tient plus sur une page lisible.</summary>
    public const int MaxItems = 50;

    private readonly List<SetlistItem> _items = [];

    private Setlist() { } // réservé à EF Core

    public Guid Id { get; private set; }
    public Guid BandId { get; private set; }
    public string Name { get; private set; } = default!;
    /// <summary>Date du concert ; null pour une setlist de travail.</summary>
    public DateOnly? Date { get; private set; }
    /// <summary>Lieu (salle, ville) ; facultatif.</summary>
    public string? Venue { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Morceaux dans l'ordre de jeu.</summary>
    public IReadOnlyList<SetlistItem> Items => [.. _items.OrderBy(i => i.Position)];

    public static Setlist Create(Guid bandId, string name, DateOnly? date, string? venue, DateTimeOffset now)
    {
        if (bandId == Guid.Empty)
            throw new DomainException("Une setlist doit appartenir à un groupe.");

        var setlist = new Setlist { Id = Guid.CreateVersion7(now), BandId = bandId, CreatedAt = now };
        setlist.UpdateDetails(name, date, venue, now);
        return setlist;
    }

    public void UpdateDetails(string name, DateOnly? date, string? venue, DateTimeOffset now)
    {
        var cleanName = Text.Required(name, NameMaxLength, "nom");
        var cleanVenue = Text.Optional(venue, VenueMaxLength, "lieu");

        Name = cleanName;
        Date = date;
        Venue = cleanVenue;
        UpdatedAt = now;
    }

    /// <summary>Vérifie une liste de morceaux sans rien modifier : 422 avant le moindre enregistrement.</summary>
    public static void EnsureValidItems(IReadOnlyList<SetlistItemChoice> items)
    {
        if (items.Count > MaxItems)
            throw new DomainException($"Une setlist compte au plus {MaxItems} morceaux.");
        if (items.Any(i => i.SongId == Guid.Empty || i.VersionId == Guid.Empty))
            throw new DomainException("Chaque ligne indique un morceau ; la version, si elle est donnée, n'est pas vide.");
        if (items.Select(i => i.SongId).Distinct().Count() != items.Count)
            throw new DomainException("Un morceau ne figure qu'une fois dans une setlist.");
        foreach (var item in items)
        {
            Text.Optional(item.Notes, SetlistItem.NotesMaxLength, "notes");
            Text.Optional(item.CommentBefore, SetlistItem.CommentMaxLength, "commentaire");
            if (item.DurationMs is < SetlistItem.MinDurationMs or > SetlistItem.MaxDurationMs)
                throw new DomainException("La durée saisie va de 1 seconde à 60 minutes.");
        }
    }

    /// <summary>
    /// Passe toutes les positions en négatif (-n … -1, dans le même ordre). Enregistré avant SetItems, dans la même
    /// transaction, pour que l'index unique (SetlistId, Position) ne bloque pas un échange de places.
    /// </summary>
    public void ParkPositions()
    {
        var position = -_items.Count;
        foreach (var item in _items.OrderBy(i => i.Position).ToList())
            item.MoveTo(position++);
    }

    /// <summary>
    /// Remplace les morceaux par cette liste, dans cet ordre (1, 2, 3…). Un morceau absent de la liste quitte
    /// la setlist ; un morceau déjà présent garde sa ligne et prend sa nouvelle place, sa version, ses notes, sa durée
    /// et son commentaire.
    /// </summary>
    public void SetItems(IReadOnlyList<SetlistItemChoice> items, DateTimeOffset now)
    {
        EnsureValidItems(items);

        var wanted = items.Select(i => i.SongId).ToHashSet();
        _items.RemoveAll(i => !wanted.Contains(i.SongId));

        for (var index = 0; index < items.Count; index++)
        {
            var choice = items[index];
            var notes = Text.Optional(choice.Notes, SetlistItem.NotesMaxLength, "notes");
            var comment = Text.Optional(choice.CommentBefore, SetlistItem.CommentMaxLength, "commentaire");
            var item = _items.SingleOrDefault(i => i.SongId == choice.SongId);
            if (item is null)
            {
                item = SetlistItem.Create(Id, choice.SongId, index + 1);
                _items.Add(item);
            }
            else item.MoveTo(index + 1);
            item.Describe(choice.VersionId, notes, choice.DurationMs, comment);
        }
        UpdatedAt = now;
    }
}