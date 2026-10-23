namespace SongVault.Application.Common.Exceptions;

/// <summary>#24 : lien qui a existé mais n'est plus valable (410), avec un message lisible par l'utilisateur.</summary>
public sealed class LinkExpiredException(string message) : Exception(message);