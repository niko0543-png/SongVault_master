namespace SongVault.Domain.Bands;

/// <summary>L'opération laisserait le groupe sans Owner. Traduite en HTTP 409.</summary>
public sealed class LastOwnerException()
    : Exception("Le groupe doit garder au moins un Owner : nommez d'abord un autre Owner.");