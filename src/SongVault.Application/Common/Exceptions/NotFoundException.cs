namespace SongVault.Application.Common.Exceptions;

public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' introuvable.")
{
    public string Resource { get; } = resource;
    public object Key { get; } = key;
}