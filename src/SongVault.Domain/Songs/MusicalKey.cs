using System.Text.RegularExpressions;

using SongVault.Domain.Common;

namespace SongVault.Domain.Songs;

/// <summary>
/// Tonalité : une note de A à G, une altération optionnelle (# ou b), puis "m" si mineur.
/// Exemples : C, F#m, Bb, Ebm.
/// </summary>
public sealed partial record MusicalKey
{
    public const int MaxLength = 4;

    // Constructeur privé : impossible de créer une tonalité invalide, il faut passer par Parse.
    private MusicalKey(string value) => Value = value;

    public string Value { get; }

    public static MusicalKey Parse(string value)
    {
        var trimmed = value?.Trim() ?? "";
        if (!Pattern().IsMatch(trimmed))
            throw new DomainException($"Tonalité invalide : « {value} ». Exemples valides : C, F#m, Bb.");
        return new MusicalKey(trimmed);
    }

    /// <summary>Chaîne vide ou null → pas de tonalité ; sinon, tonalité validée.</summary>
    public static MusicalKey? ParseOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : Parse(value);

    public override string ToString() => Value;

    // Expression régulière générée à la compilation (.NET 7+)
    [GeneratedRegex("^[A-G](#|b)?m?$")]
    private static partial Regex Pattern();
}