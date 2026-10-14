using SongVault.Domain.Common;

namespace SongVault.Domain.Releases;

/// <summary>
/// Une sortie du groupe (single, EP, album) et ses pistes dans l'ordre (ADR 0016).
/// Un même morceau peut figurer sur plusieurs sorties, chaque fois dans la version choisie.
/// </summary>
public sealed class Release
{
    public const int TitleMaxLength = 200;
    public const int MaxTracks = 100;

    private readonly List<ReleaseTrack> _tracks = [];

    private Release() { } // réservé à EF Core

    public Guid Id { get; private set; }
    public Guid BandId { get; private set; }
    public string Title { get; private set; } = default!;
    public ReleaseType Type { get; private set; }
    /// <summary>Date de sortie ; null tant qu'elle n'est pas fixée.</summary>
    public DateOnly? ReleaseDate { get; private set; }
    /// <summary>Clé de la pochette dans le stockage ; null sans pochette.</summary>
    public string? CoverStorageKey { get; private set; }
    public string? CoverContentType { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Pistes dans l'ordre de leur numéro.</summary>
    public IReadOnlyList<ReleaseTrack> Tracks => [.. _tracks.OrderBy(t => t.TrackNumber)];

    public static Release Create(Guid bandId, string title, ReleaseType type, DateOnly? releaseDate, DateTimeOffset now)
    {
        if (bandId == Guid.Empty)
            throw new DomainException("Une sortie doit appartenir à un groupe.");

        var release = new Release { Id = Guid.CreateVersion7(now), BandId = bandId, CreatedAt = now };
        release.UpdateDetails(title, type, releaseDate, now);
        return release;
    }

    public void UpdateDetails(string title, ReleaseType type, DateOnly? releaseDate, DateTimeOffset now)
    {
        var cleanTitle = Text.Required(title, TitleMaxLength, "titre");
        if (!Enum.IsDefined(type))
            throw new DomainException("Le type de sortie doit être Single, EP ou Album.");

        Title = cleanTitle;
        Type = type;
        ReleaseDate = releaseDate;
        UpdatedAt = now;
    }

    /// <summary>Remplace la pochette. Renvoie la clé de l'ancienne, à effacer du stockage après l'enregistrement.</summary>
    public string? SetCover(string storageKey, string contentType, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || string.IsNullOrWhiteSpace(contentType))
            throw new DomainException("Une pochette a une clé de stockage et un type.");

        var previous = CoverStorageKey;
        CoverStorageKey = storageKey;
        CoverContentType = contentType;
        UpdatedAt = now;
        return previous;
    }

    /// <summary>Retire la pochette. Renvoie sa clé (null s'il n'y en avait pas).</summary>
    public string? RemoveCover(DateTimeOffset now)
    {
        var previous = CoverStorageKey;
        CoverStorageKey = null;
        CoverContentType = null;
        if (previous is not null) UpdatedAt = now;
        return previous;
    }

    /// <summary>Vérifie une liste de pistes sans rien modifier : 422 avant le moindre enregistrement.</summary>
    public static void EnsureValidTracks(IReadOnlyList<ReleaseTrackChoice> tracks)
    {
        if (tracks.Count > MaxTracks)
            throw new DomainException($"Une sortie compte au plus {MaxTracks} pistes.");
        if (tracks.Any(t => t.SongId == Guid.Empty || t.VersionId == Guid.Empty))
            throw new DomainException("Chaque piste indique un morceau et une version.");
        if (tracks.Select(t => t.SongId).Distinct().Count() != tracks.Count)
            throw new DomainException("Un morceau ne figure qu'une fois sur une sortie.");
    }

    /// <summary>
    /// Passe tous les numéros en négatif (-n … -1, dans le même ordre). Enregistré avant SetTracks, dans la même
    /// transaction, pour que l'index unique (ReleaseId, TrackNumber) ne bloque pas un échange de places.
    /// </summary>
    public void ParkTrackNumbers()
    {
        var number = -_tracks.Count;
        foreach (var track in _tracks.OrderBy(t => t.TrackNumber).ToList())
            track.Place(number++, track.VersionId);
    }

    /// <summary>
    /// Remplace les pistes par cette liste, dans cet ordre (1, 2, 3…). Un morceau absent de la liste quitte
    /// la sortie ; un morceau déjà présent garde sa ligne et prend sa nouvelle place et sa version.
    /// </summary>
    public void SetTracks(IReadOnlyList<ReleaseTrackChoice> tracks, DateTimeOffset now)
    {
        EnsureValidTracks(tracks);

        var wanted = tracks.Select(t => t.SongId).ToHashSet();
        _tracks.RemoveAll(t => !wanted.Contains(t.SongId));

        for (var i = 0; i < tracks.Count; i++)
        {
            var choice = tracks[i];
            var existing = _tracks.SingleOrDefault(t => t.SongId == choice.SongId);
            if (existing is null) _tracks.Add(ReleaseTrack.Create(Id, choice.SongId, choice.VersionId, i + 1));
            else existing.Place(i + 1, choice.VersionId);
        }
        UpdatedAt = now;
    }
}