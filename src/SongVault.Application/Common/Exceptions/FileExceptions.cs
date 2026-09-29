namespace SongVault.Application.Common.Exceptions;

public sealed class UnsupportedFileException(string message) : Exception(message);

public sealed class FileTooLargeException(long maxBytes)
    : Exception($"Le fichier dépasse la taille maximale de {maxBytes / (1024 * 1024)} Mo.");