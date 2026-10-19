using System.Text.RegularExpressions;

using SongVault.Domain.Common;

namespace SongVault.Domain.Songs;

/// <summary>
/// ISRC : le code international de l'enregistrement (#22), porté par la version. Pays (2 lettres), déclarant
/// (3 lettres ou chiffres), année (2 chiffres) et numéro (5 chiffres) : FR-Z03-26-00001. On accepte la saisie
/// avec ou sans tirets et espaces ; on garde les 12 caractères.
/// </summary>
public sealed partial record Isrc
{
    public const int MaxLength = 12;

    private Isrc(string value) => Value = value;

    /// <summary>Les 12 caractères, sans tiret : FRZ032600001.</summary>
    public string Value { get; }

    /// <summary>Forme lisible : FR-Z03-26-00001.</summary>
    public string Display => $"{Value[..2]}-{Value[2..5]}-{Value[5..7]}-{Value[7..]}";

    public static Isrc Parse(string value)
    {
        var compact = Separators().Replace(value ?? "", "").ToUpperInvariant();
        if (!Pattern().IsMatch(compact))
            throw new DomainException($"ISRC invalide : « {value} ». Format attendu : FR-Z03-26-00001.");
        return new Isrc(compact);
    }

    /// <summary>Chaîne vide ou null → pas d'ISRC ; sinon, code validé.</summary>
    public static Isrc? ParseOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : Parse(value);

    public override string ToString() => Display;

    [GeneratedRegex("^[A-Z]{2}[A-Z0-9]{3}[0-9]{7}$")]
    private static partial Regex Pattern();

    [GeneratedRegex("[\\s\\-]")]
    private static partial Regex Separators();
}