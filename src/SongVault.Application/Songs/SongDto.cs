using SongVault.Domain.Songs;

namespace SongVault.Application.Songs;

/// <summary>
/// DTO représentant une chanson avec ses informations de base.
/// </summary>
/// <param name="Id">Identifiant unique de la chanson</param>
/// <param name="Title">Titre de la chanson</param>
/// <param name="Artist">Artiste ou compositeur (optionnel)</param>
/// <param name="Description">Description ou notes sur la chanson (optionnel)</param>
/// <param name="CreatedAt">Date et heure de création de la chanson</param>
/// <param name="UpdatedAt">Date et heure de la dernière mise à jour</param>
/// <param name="Badge">Pastille pour l'utilisateur qui lit la liste (#11) ; None hors de la liste</param>
/// <param name="TablatureVersionId">#34 : version de la tablature à ouvrir depuis la liste (la plus récente qui en a une lisible) ; null hors de la liste</param>
/// <param name="TablatureFileId">#34 : cette tablature (la première envoyée dans la version)</param>
public sealed record SongDto(
    Guid Id, string Title, string? Artist, string? Description,
     DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, SongBadge Badge = SongBadge.None,
    Guid? TablatureVersionId = null, Guid? TablatureFileId = null)
{
    public static SongDto From(Song song) =>
        new(song.Id, song.Title, song.Artist, song.Description, song.CreatedAt, song.UpdatedAt);
}