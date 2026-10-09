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
public sealed record SongDto(
    Guid Id, string Title, string? Artist, string? Description,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static SongDto From(Song song) =>
        new(song.Id, song.Title, song.Artist, song.Description, song.CreatedAt, song.UpdatedAt);
}