namespace SongVault.Application.Common.Exceptions;

/// <summary>Demande incompatible avec l'état actuel (409), avec un message lisible par l'utilisateur.</summary>
public sealed class ConflictException(string message) : Exception(message);