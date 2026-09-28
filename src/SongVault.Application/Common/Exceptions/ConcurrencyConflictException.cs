namespace SongVault.Application.Common.Exceptions;

public sealed class ConcurrencyConflictException(Exception? inner = null)
    : Exception("La ressource a été modifiée par une autre requête. Réessayez.", inner);