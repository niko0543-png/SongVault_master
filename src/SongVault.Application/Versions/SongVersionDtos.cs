using SongVault.Application.Files;
using SongVault.Domain.Songs;

namespace SongVault.Application.Versions;

/// <summary>
/// DTO représentant une version complète d'une chanson avec tous ses détails.
/// </summary>
/// <param name="Id">Identifiant unique de la version</param>
/// <param name="SongId">Identifiant de la chanson parente</param>
/// <param name="Number">Numéro de version séquentiel</param>
/// <param name="Title">Titre de la version</param>
/// <param name="Status">Statut de la version (Demo, Final, etc.)</param>
/// <param name="Notes">Notes additionnelles sur la version (optionnel)</param>
/// <param name="Lyrics">Paroles de la version (optionnel)</param>
/// <param name="Bpm">Tempo en battements par minute (optionnel)</param>
/// <param name="Key">Tonalité musicale de la version (optionnel)</param>
/// <param name="CreatedAt">Date et heure de création de la version</param>
/// <param name="UpdatedAt">Date et heure de la dernière mise à jour</param>
/// <param name="Files">Collection des fichiers associés à cette version</param>
public sealed record SongVersionDto(
    Guid Id, Guid SongId, int Number, string Title, SongVersionStatus Status,
    string? Notes, string? Lyrics,
    int? Bpm, string? Key,                                     // NOUVEAU (juste après Lyrics)
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<SongFileDto> Files)
{
    public static SongVersionDto From(SongVersion v) =>
        new(v.Id, v.SongId, v.Number, v.Title, v.Status, v.Notes, v.Lyrics,
            v.Bpm, v.Key?.Value,
            v.CreatedAt, v.UpdatedAt,
            [.. v.Files.Select(SongFileDto.From)]);
}

/// <summary>
/// DTO résumé d'une version de chanson contenant uniquement les informations essentielles.
/// Utile pour les listes ou les aperçus sans charger tous les détails et fichiers.
/// </summary>
/// <param name="Id">Identifiant unique de la version</param>
/// <param name="Number">Numéro de version séquentiel</param>
/// <param name="Title">Titre de la version</param>
/// <param name="Status">Statut de la version (Demo, Final, etc.)</param>
/// <param name="CreatedAt">Date et heure de création de la version</param>
public sealed record SongVersionSummaryDto(Guid Id, int Number, string Title, SongVersionStatus Status, DateTimeOffset CreatedAt);

