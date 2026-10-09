namespace SongVault.Domain.Bands;

public enum BandRole { Owner, Member, Guest }

public static class BandRoleExtensions
{
    /// <summary>Vrai si ce rôle donne au moins les droits de <paramref name="minimum"/> (Owner ⊃ Member ⊃ Guest).</summary>
    public static bool Allows(this BandRole role, BandRole minimum) => Rank(role) >= Rank(minimum);

    // Rang explicite : l'ordre des valeurs de l'enum (Owner = 0) ne dit rien des droits
    private static int Rank(BandRole role) => role switch
    {
        BandRole.Owner => 3,
        BandRole.Member => 2,
        BandRole.Guest => 1,
        _ => 0,
    };
}