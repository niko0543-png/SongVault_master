using System.Text.RegularExpressions;

using SongVault.Domain.Common;

namespace SongVault.Domain.Releases;

/// <summary>
/// Code-barres d'une sortie (#38) : UPC (12 chiffres) ou EAN (13 chiffres), donné par le distributeur. On accepte la
/// saisie avec espaces ou tirets ; la clé de contrôle (dernier chiffre) est vérifiée.
/// </summary>
public sealed partial record Upc
{
    public const int MaxLength = 13;

    private Upc(string value) => Value = value;

    public string Value { get; }

    public static Upc Parse(string value)
    {
        var compact = Separators().Replace(value ?? "", "");
        if (!Pattern().IsMatch(compact))
            throw new DomainException($"Code UPC/EAN invalide : « {value} ». Il compte 12 ou 13 chiffres.");

        // Clé GTIN : depuis la droite (sans la clé), poids 3, 1, 3, 1…
        var sum = 0;
        var weight = 3;
        for (var i = compact.Length - 2; i >= 0; i--, weight = 4 - weight)
            sum += (compact[i] - '0') * weight;
        if ((10 - sum % 10) % 10 != compact[^1] - '0')
            throw new DomainException($"Code UPC/EAN invalide : « {value} ». La clé de contrôle ne correspond pas (vérifiez les chiffres).");
        return new Upc(compact);
    }

    /// <summary>Chaîne vide ou null → pas de code ; sinon, code validé.</summary>
    public static Upc? ParseOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : Parse(value);

    public override string ToString() => Value;

    [GeneratedRegex("^[0-9]{12,13}$")]
    private static partial Regex Pattern();

    [GeneratedRegex("[\\s\\-]")]
    private static partial Regex Separators();
}
