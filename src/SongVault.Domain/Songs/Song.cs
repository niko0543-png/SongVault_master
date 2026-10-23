using System.Globalization;

using SongVault.Domain.Common;
using SongVault.Domain.Trash;

namespace SongVault.Domain.Songs;

public sealed class Song : ISoftDeletable
{
    private readonly List<SongVersion> _versions = [];
    private readonly List<Credit> _credits = [];
    public const int TitleMaxLength = 200;
    public const int ArtistMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int MaxCredits = 30;

    public int LastVersionNumber { get; private set; }
    public IReadOnlyCollection<SongVersion> Versions => _versions.AsReadOnly();
    /// <summary>Les ayants droit (#22), dans l'ordre de la liste enregistrée.</summary>
    public IReadOnlyList<Credit> Credits => _credits.OrderBy(c => c.Position).ToList();
    private Song() { } // réservé à EF Core (jeudi) ; le reste du code passe par Create

    public Guid Id { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Artist { get; private set; }
    public string? Description { get; private set; }
    /// <summary>Code de l'œuvre (#22) ; l'enregistrement a le sien, l'ISRC, sur chaque version.</summary>
    public Iswc? Iswc { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid BandId { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedById { get; private set; }
    public bool IsDeleted => DeletedAt is not null;
    /// <summary>
    /// Sortie où le morceau est paru (#38, ADR 0044) ; null tant qu'il est en travail. Un morceau sorti quitte la liste
    /// des morceaux pour l'onglet Sorties. Supprimer la sortie le remet en travail (clé étrangère SET NULL).
    /// </summary>
    public Guid? ReleaseId { get; private set; }
    /// <summary>Quand ses infos de sortie ont été enregistrées ; n'a de sens que si ReleaseId est renseigné.</summary>
    public DateTimeOffset? ReleasedAt { get; private set; }
    public bool IsReleased => ReleaseId is not null;

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
    /// Enregistre l'ISWC et l'ensemble des crédits (#22), qui remplace l'ancien. Règle des 100 % : une liste vide
    /// est permise (droits pas encore renseignés) ; sinon les parts font exactement 100 %. Tout est validé avant
    /// de toucher au morceau. Les crédits à charger : tous.
    /// </summary>
    public void SetRights(Iswc? iswc, IReadOnlyList<CreditChoice> credits, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(credits);
        if (credits.Count > MaxCredits)
            throw new DomainException($"Un morceau a au plus {MaxCredits} crédits.");

        var clean = new List<(string Name, CreditRole Role, decimal Share)>(credits.Count);
        foreach (var credit in credits)
        {
            var name = Text.Required(credit.Name, Credit.NameMaxLength, "nom de l'ayant droit");
            if (!Enum.IsDefined(credit.Role))
                throw new DomainException($"Rôle inconnu : {credit.Role}.");
            if (credit.SharePercent is <= 0 or > 100)
                throw new DomainException($"La part de {name} doit être comprise entre 0,01 et 100 %.");
            if (decimal.Round(credit.SharePercent, 2) != credit.SharePercent)
                throw new DomainException($"La part de {name} a au plus deux décimales.");
            if (clean.Any(c => c.Role == credit.Role && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new DomainException($"{name} apparaît deux fois avec le même rôle : regroupez ses parts.");
            clean.Add((name, credit.Role, credit.SharePercent));
        }

        var total = clean.Sum(c => c.Share);
        if (clean.Count > 0 && total != 100m)
            throw new DomainException($"Le total des parts doit faire 100 % ; il fait {total.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',')} %.");

        _credits.Clear();
        for (var i = 0; i < clean.Count; i++)
            _credits.Add(Credit.Create(Id, i + 1, clean[i].Name, clean[i].Role, clean[i].Share));
        Iswc = iswc;
        UpdatedAt = now;
    }

    /// <summary>
    /// La version qui peut sortir (#38) : la dernière version en place, si elle est au statut Final ; sinon une
    /// DomainException qui dit pourquoi. Les versions à charger : toutes celles qui sont en place.
    /// </summary>
    public SongVersion EnsureReleasable(Guid versionId)
    {
        if (IsDeleted) throw new DomainException("Ce morceau est dans la corbeille.");
        var version = _versions.SingleOrDefault(v => v.Id == versionId && !v.IsDeleted)
                      ?? throw new DomainException("Cette version n'appartient pas au morceau.");
        var latest = _versions.Where(v => !v.IsDeleted).MaxBy(v => v.Number)!;
        if (latest.Id != version.Id)
            throw new DomainException($"Seule la dernière version (v{latest.Number}) peut sortir.");
        if (version.Status != SongVersionStatus.Final)
            throw new DomainException("Passez d'abord cette version au statut Final : c'est elle qui sort.");
        return version;
    }

    /// <summary>Le morceau est sorti sur cette sortie (#38) : il passe dans l'onglet Sorties.</summary>
    public void MarkReleased(Guid releaseId, DateTimeOffset now)
    {
        if (releaseId == Guid.Empty) throw new DomainException("Un morceau sort sur une sortie.");
        if (IsReleased) throw new DomainException("Ce morceau est déjà sorti.");
        ReleaseId = releaseId;
        ReleasedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Remet le morceau en travail (#38) : il revient dans la liste des morceaux. La sortie et ses pistes restent.</summary>
    public bool Unrelease(DateTimeOffset now)
    {
        if (!IsReleased) return false;
        ReleaseId = null;
        ReleasedAt = null;
        UpdatedAt = now;
        return true;
    }

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