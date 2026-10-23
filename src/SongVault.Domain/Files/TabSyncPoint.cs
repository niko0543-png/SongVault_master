namespace SongVault.Domain.Files;

/// <summary>
/// Un point de synchronisation d'une tablature avec sa maquette (#29, ADR 0036) : tel endroit de la partition
/// tombe à tel instant de l'enregistrement. Reprend le FlatSyncPoint d'alphaTab, qui interpole entre les points.
/// </summary>
/// <param name="BarIndex">Mesure, à partir de 0.</param>
/// <param name="BarOccurence">Passage dans la mesure quand elle est reprise (0 = la première fois).</param>
/// <param name="BarPosition">Place dans la mesure, de 0 (son début) à 1 (sa fin).</param>
/// <param name="MillisecondOffset">Instant de la maquette, en millisecondes depuis son début.</param>
public sealed record TabSyncPoint(int BarIndex, int BarOccurence, double BarPosition, int MillisecondOffset)
{
    public const int MaxBarIndex = 9_999;
    public const int MaxBarOccurence = 99;
    /// <summary>Six heures : bien au-delà de la plus longue maquette acceptée.</summary>
    public const int MaxMillisecondOffset = 6 * 60 * 60 * 1000;
}