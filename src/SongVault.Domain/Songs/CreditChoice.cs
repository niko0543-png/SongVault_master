namespace SongVault.Domain.Songs;

/// <summary>Un crédit tel qu'on l'envoie : qui, à quel titre, et sa part en pourcentage (deux décimales au plus).</summary>
public sealed record CreditChoice(string Name, CreditRole Role, decimal SharePercent);