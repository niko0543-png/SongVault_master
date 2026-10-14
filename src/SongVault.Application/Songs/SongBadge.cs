namespace SongVault.Application.Songs;

/// <summary>
/// Pastille d'un morceau dans la liste, pour l'utilisateur qui la lit (#11). Ne compte que les modifications
/// des autres membres, faites depuis sa dernière ouverture du morceau, ou depuis son arrivée dans le groupe.
/// </summary>
public enum SongBadge
{
    None,
    /// <summary>Jamais ouvert, et modifié par un autre membre.</summary>
    New,
    /// <summary>Modifié par un autre membre depuis la dernière ouverture.</summary>
    Updated,
}