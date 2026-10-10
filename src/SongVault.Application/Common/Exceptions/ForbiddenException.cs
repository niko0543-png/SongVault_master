namespace SongVault.Application.Common.Exceptions;

/// <summary>Membre du groupe, mais son rôle ne permet pas l'action. Traduite en HTTP 403.</summary>
public sealed class ForbiddenException(string message) : Exception(message);