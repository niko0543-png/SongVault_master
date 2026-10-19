namespace SongVault.Domain.Setlists;

/// <summary>
/// Une ligne de setlist telle qu'on l'envoie : le morceau, sa version (null = la dernière), ses notes et sa durée
/// saisie (null = celle du fichier audio). La place vient de l'ordre dans la liste.
/// </summary>
public sealed record SetlistItemChoice(Guid SongId, Guid? VersionId = null, string? Notes = null, int? DurationMs = null);