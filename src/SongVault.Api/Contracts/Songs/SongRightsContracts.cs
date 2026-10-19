using System.ComponentModel.DataAnnotations;

using SongVault.Application.Songs.Rights;
using SongVault.Domain.Songs;

namespace SongVault.Api.Contracts.Songs;

/// <summary>Un ayant droit : nom, rôle (Author, Composer, Arranger, Publisher) et part en pourcentage, deux décimales au plus.</summary>
public sealed record CreditRequest(
    [Required, MaxLength(Credit.NameMaxLength)] string Name,
    [Required] CreditRole? Role,
    [Required] decimal? SharePercent);

/// <summary>
/// L'ISWC (vide = aucun) et tous les crédits, qui remplacent les anciens. Liste vide permise ; sinon les parts
/// font exactement 100 % (règle vérifiée par le domaine : 422 sinon).
/// </summary>
public sealed record SetSongRightsRequest(
    [MaxLength(SetSongRightsRequest.CodeInputMaxLength)] string? Iswc,
    [Required, MaxLength(Song.MaxCredits)] IReadOnlyList<CreditRequest> Credits)
{
    /// <summary>La saisie peut garder espaces, points et tirets : on vérifie le format après les avoir ôtés.</summary>
    public const int CodeInputMaxLength = 30;

    public SetSongRightsCommand ToCommand(Guid songId)
        => new(songId, Iswc, [.. Credits.Select(c => new CreditChoice(c.Name, c.Role!.Value, c.SharePercent!.Value))]);
}

/// <summary>L'ISRC d'une version (vide = aucun), avec ou sans tirets : FR-Z03-26-00001.</summary>
public sealed record SetVersionIsrcRequest([MaxLength(SetSongRightsRequest.CodeInputMaxLength)] string? Isrc);

public sealed record CreditResponse(string Name, CreditRole Role, decimal SharePercent);

public sealed record VersionIsrcResponse(Guid Id, int Number, string Title, string? Isrc)
{
    public static VersionIsrcResponse From(VersionIsrcDto d) => new(d.Id, d.Number, d.Title, d.Isrc);
}

/// <summary>Les droits du morceau : ISWC, crédits dans l'ordre et leur total, ISRC de chaque version en place.</summary>
public sealed record SongRightsResponse(
    Guid SongId, string SongTitle, string? Iswc, IReadOnlyList<CreditResponse> Credits, decimal TotalPercent,
    IReadOnlyList<VersionIsrcResponse> Versions)
{
    public static SongRightsResponse From(SongRightsDto d)
        => new(d.SongId, d.SongTitle, d.Iswc, [.. d.Credits.Select(c => new CreditResponse(c.Name, c.Role, c.SharePercent))],
            d.TotalPercent, [.. d.Versions.Select(VersionIsrcResponse.From)]);
}