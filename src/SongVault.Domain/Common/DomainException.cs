namespace SongVault.Domain.Common;

/// <summary>Violation d'une règle métier. Traduite en HTTP 422 à partir de la semaine 2.</summary>
public sealed class DomainException(string message) : Exception(message);
