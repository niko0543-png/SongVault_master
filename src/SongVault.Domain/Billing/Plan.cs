using SongVault.Domain.Common;

namespace SongVault.Domain.Billing;

/// <summary>
/// Une formule (ADR 0023). Les limites vivent dans la table Plans, pas dans le code : les changer, c'est une
/// migration ou un UPDATE, sans nouvelle version. Le prix affiché doit rester celui du prix Stripe de la formule.
/// </summary>
public sealed class Plan
{
    /// <summary>Formule des groupes sans abonnement en cours.</summary>
    public const string FreeId = "free";
    public const int IdMaxLength = 20;
    public const int NameMaxLength = 50;

    private Plan() { } // réservé à EF Core

    public string Id { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    /// <summary>Taille totale des fichiers des morceaux du groupe (originaux), en octets.</summary>
    public long StorageQuotaBytes { get; private set; }
    /// <summary>Groupes sur cette formule dont un même compte peut être Owner ; null : sans limite.</summary>
    public int? MaxBandsPerOwner { get; private set; }
    /// <summary>Prix affiché, en centimes d'euro TTC par mois ; 0 pour la formule gratuite.</summary>
    public int MonthlyPriceCents { get; private set; }
    public int SortOrder { get; private set; }

    public bool IsFree => Id == FreeId;

    public static Plan Create(string id, string name, long storageQuotaBytes, int? maxBandsPerOwner,
        int monthlyPriceCents, int sortOrder)
    {
        if (storageQuotaBytes <= 0) throw new DomainException("Le quota de stockage doit être positif.");
        if (maxBandsPerOwner is < 1) throw new DomainException("Une formule permet au moins un groupe.");
        if (monthlyPriceCents < 0) throw new DomainException("Un prix ne peut pas être négatif.");
        return new Plan
        {
            Id = Text.Required(id, IdMaxLength, "identifiant de formule"),
            Name = Text.Required(name, NameMaxLength, "nom de formule"),
            StorageQuotaBytes = storageQuotaBytes,
            MaxBandsPerOwner = maxBandsPerOwner,
            MonthlyPriceCents = monthlyPriceCents,
            SortOrder = sortOrder,
        };
    }

    /// <summary>Faux si ces octets en plus dépassent le quota.</summary>
    public bool HasRoomFor(long usedBytes, long incomingBytes) => usedBytes + incomingBytes <= StorageQuotaBytes;
}