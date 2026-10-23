using SongVault.Domain.Setlists;

namespace SongVault.Application.Setlists;

/// <summary>Une setlist dans la liste du groupe.</summary>
public sealed record SetlistSummaryDto(Guid Id, string Name, DateOnly? Date, string? Venue, int ItemCount);

/// <summary>Une setlist et ses lignes dans l'ordre de jeu, avec la durée totale calculée ici, côté API.</summary>
public sealed record SetlistDto(
    Guid Id, string Name, DateOnly? Date, string? Venue,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<SetlistItemDto> Items)
{
    /// <summary>Somme exacte des durées connues, en millisecondes.</summary>
    public long TotalDurationMs => Items.Sum(i => (long)(i.DurationMs ?? 0));

    /// <summary>Lignes sans durée (ni saisie, ni fichier audio analysé) : le total les ignore, l'écran le signale.</summary>
    public int MissingDurationCount => Items.Count(i => i.DurationMs is null);

    /// <summary>Setlist qui vient d'être créée : pas encore de morceau.</summary>
    public static SetlistDto FromNew(Setlist s) => new(s.Id, s.Name, s.Date, s.Venue, s.CreatedAt, s.UpdatedAt, []);
}

/// <summary>
/// Une ligne. VersionId est la version choisie (null = la dernière) ; PlayedVersionId celle qui sera jouée,
/// dont viennent Key, Bpm et AudioDurationMs. Versions liste les versions du morceau, pour en choisir une.
/// CommentBefore se lit entre la ligne précédente et celle-ci (ADR 0046).
/// </summary>
public sealed record SetlistItemDto(
    int Position, Guid SongId, string SongTitle, Guid? VersionId, Guid? PlayedVersionId,
    IReadOnlyList<SetlistVersionOptionDto> Versions, string? Key, int? Bpm, string? Notes,
    int? ManualDurationMs, int? AudioDurationMs, string? CommentBefore = null)
{
    /// <summary>La durée saisie prime ; sinon celle du fichier audio ; sinon inconnue.</summary>
    public int? DurationMs => ManualDurationMs ?? AudioDurationMs;
}

public sealed record SetlistVersionOptionDto(Guid Id, int Number, string Title);

public sealed record CreateSetlistCommand(string Name, DateOnly? Date, string? Venue);

public sealed record UpdateSetlistCommand(Guid Id, string Name, DateOnly? Date, string? Venue);

/// <summary>La liste complète des lignes, dans l'ordre de jeu voulu.</summary>
public sealed record SetSetlistItemsCommand(Guid SetlistId, IReadOnlyList<SetlistItemChoice> Items);