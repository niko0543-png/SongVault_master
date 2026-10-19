using System.Text.RegularExpressions;

using SongVault.Domain.Common;

namespace SongVault.Domain.Songs;

/// <summary>
/// ISWC : le code international de l'œuvre (#22), porté par le morceau. Un T, neuf chiffres et une clé de contrôle,
/// écrit T-034.524.680-1. On accepte la saisie avec ou sans tirets, points et espaces.
/// </summary>
public sealed partial record Iswc
{
    /// <summary>Longueur de la forme écrite, T-034.524.680-1.</summary>
    public const int MaxLength = 15;

    private Iswc(string value) => Value = value;

    /// <summary>Forme écrite : T-034.524.680-1.</summary>
    public string Value { get; }

    public static Iswc Parse(string value)
    {
        var compact = Separators().Replace(value ?? "", "").ToUpperInvariant();
        if (!Pattern().IsMatch(compact))
            throw new DomainException($"ISWC invalide : « {value} ». Format attendu : T-034.524.680-1.");

        // Clé : 1 (pour le T) + somme des chiffres pondérés par leur rang (1 à 9), puis complément à 10
        var sum = 1;
        for (var i = 1; i <= 9; i++) sum += i * (compact[i] - '0');
        var check = (10 - sum % 10) % 10;
        if (compact[10] - '0' != check)
            throw new DomainException($"ISWC invalide : « {value} ». La clé de contrôle ne correspond pas (vérifiez les chiffres).");

        return new Iswc($"T-{compact[1..4]}.{compact[4..7]}.{compact[7..10]}-{compact[10]}");
    }

    /// <summary>Chaîne vide ou null → pas d'ISWC ; sinon, code validé.</summary>
    public static Iswc? ParseOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : Parse(value);

    public override string ToString() => Value;

    [GeneratedRegex("^T[0-9]{10}$")]
    private static partial Regex Pattern();

    [GeneratedRegex("[\\s.\\-]")]
    private static partial Regex Separators();
}