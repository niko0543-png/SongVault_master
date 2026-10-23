namespace SongVault.Domain.Setlists;

/// <summary>
/// Un morceau d'une setlist : à quelle place, dans quelle version (facultative), avec quelles notes de scène.
/// Clé (SetlistId, SongId) : un morceau figure au plus une fois dans une setlist.
/// </summary>
public sealed class SetlistItem
{
    public const int NotesMaxLength = 500;
    public const int CommentMaxLength = 500;
    public const int MinDurationMs = 1_000;
    public const int MaxDurationMs = 60 * 60 * 1_000;

    private SetlistItem() { } // réservé à EF Core

    public Guid SetlistId { get; private set; }
    public Guid SongId { get; private set; }
    /// <summary>Version jouée ; null = la dernière version du morceau, relue à chaque affichage.</summary>
    public Guid? VersionId { get; private set; }
    /// <summary>Place dans la setlist, à partir de 1. Négative le temps d'un réordonnancement (voir Setlist.ParkPositions).</summary>
    public int Position { get; private set; }
    /// <summary>Notes de scène : « enchaîner », « capo 2 », « intro rallongée »…</summary>
    public string? Notes { get; private set; }
    /// <summary>Durée saisie à la main, en millisecondes ; prime sur la durée du fichier audio (#15).</summary>
    public int? DurationMs { get; private set; }
    /// <summary>
    /// Ce qui se passe juste avant ce morceau (ADR 0046) : « présenter le groupe », « changement de guitare »…
    /// Affiché entre le morceau précédent et celui-ci ; il suit le morceau quand on le déplace.
    /// </summary>
    public string? CommentBefore { get; private set; }

    internal static SetlistItem Create(Guid setlistId, Guid songId, int position)
        => new() { SetlistId = setlistId, SongId = songId, Position = position };

    internal void MoveTo(int position) => Position = position;

    internal void Describe(Guid? versionId, string? notes, int? durationMs, string? commentBefore)
    {
        VersionId = versionId;
        Notes = notes;
        DurationMs = durationMs;
        CommentBefore = commentBefore;
    }
}