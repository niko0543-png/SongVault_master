namespace SongVault.Domain.Common;

internal static class Text
{
    public static string Required(string? value, int maxLength, string field)
        => Optional(value, maxLength, field)
           ?? throw new DomainException($"Le champ {field} est obligatoire.");

    public static string? Optional(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > maxLength)
            throw new DomainException($"Le champ {field} ne peut pas dépasser {maxLength} caractères.");
        return trimmed;
    }
}