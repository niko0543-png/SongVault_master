namespace SongVault.Application.Common.Exceptions;

/// <summary>Le fournisseur d'e-mail a refusé l'envoi ou n'a pas répondu.</summary>
public sealed class EmailDeliveryException(string message, Exception? inner = null) : Exception(message, inner);