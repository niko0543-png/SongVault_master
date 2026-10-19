using System.ComponentModel.DataAnnotations;

using SongVault.Application.Setlists;
using SongVault.Domain.Setlists;

namespace SongVault.Api.Contracts.Setlists;

/// <summary>Nom, date du concert (facultative pour une setlist de travail) et lieu (facultatif).</summary>
public sealed record SaveSetlistRequest(
    [Required, MaxLength(Setlist.NameMaxLength)] string Name,
    DateOnly? Date,
    [MaxLength(Setlist.VenueMaxLength)] string? Venue);

/// <summary>
/// Une ligne : le morceau, la version jouée (absente = la dernière), les notes de scène et la durée saisie
/// en millisecondes (absente = celle du fichier audio).
/// </summary>
public sealed record SetlistItemRequest(
    [Required] Guid? SongId,
    Guid? VersionId,
    [MaxLength(SetlistItem.NotesMaxLength)] string? Notes,
    [Range(SetlistItem.MinDurationMs, SetlistItem.MaxDurationMs)] int? DurationMs);

/// <summary>Toutes les lignes, dans l'ordre de jeu : la première de la liste ouvre le concert.</summary>
public sealed record SetSetlistItemsRequest([Required, MaxLength(Setlist.MaxItems)] IReadOnlyList<SetlistItemRequest> Items);

public sealed record SetlistSummaryResponse(Guid Id, string Name, DateOnly? Date, string? Venue, int ItemCount)
{
    public static SetlistSummaryResponse From(SetlistSummaryDto d) => new(d.Id, d.Name, d.Date, d.Venue, d.ItemCount);
}

public sealed record SetlistVersionOptionResponse(Guid Id, int Number, string Title);

/// <summary>
/// Une ligne. VersionId : version choisie (null = la dernière) ; PlayedVersionId : version jouée, dont viennent
/// Key, Bpm et AudioDurationMs. DurationMs : la durée retenue (saisie, sinon audio, sinon null).
/// </summary>
public sealed record SetlistItemResponse(
    int Position, Guid SongId, string SongTitle, Guid? VersionId, Guid? PlayedVersionId,
    IReadOnlyList<SetlistVersionOptionResponse> Versions, string? Key, int? Bpm, string? Notes,
    int? ManualDurationMs, int? AudioDurationMs, int? DurationMs);

/// <summary>Une setlist ; TotalDurationMs additionne les durées connues, MissingDurationCount compte les autres.</summary>
public sealed record SetlistResponse(
    Guid Id, string Name, DateOnly? Date, string? Venue, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<SetlistItemResponse> Items, long TotalDurationMs, int MissingDurationCount)
{
    public static SetlistResponse From(SetlistDto d) =>
        new(d.Id, d.Name, d.Date, d.Venue, d.CreatedAt, d.UpdatedAt,
            [.. d.Items.Select(i => new SetlistItemResponse(i.Position, i.SongId, i.SongTitle, i.VersionId, i.PlayedVersionId,
                [.. i.Versions.Select(v => new SetlistVersionOptionResponse(v.Id, v.Number, v.Title))],
                i.Key, i.Bpm, i.Notes, i.ManualDurationMs, i.AudioDurationMs, i.DurationMs))],
            d.TotalDurationMs, d.MissingDurationCount);
}