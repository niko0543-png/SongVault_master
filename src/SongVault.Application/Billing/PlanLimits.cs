using SongVault.Application.Abstractions;
using SongVault.Application.Common;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Billing;

namespace SongVault.Application.Billing;

/// <summary>Jauge de stockage : ce qu'utilise le groupe et ce que permet sa formule.</summary>
public sealed record BandUsageDto(string PlanId, string PlanName, long UsedBytes, long QuotaBytes);

/// <summary>Les limites de la formule du groupe (ADR 0023) : stockage, et groupes gratuits par Owner.</summary>
public sealed class PlanLimits(IBillingRepository billing)
{
    /// <summary>Formule qui s'applique au groupe : celle de son abonnement s'il court, sinon la gratuite.</summary>
    public async Task<Plan> EffectivePlanAsync(Guid bandId, CancellationToken ct)
    {
        var subscription = await billing.GetSubscriptionAsync(bandId, ct);
        var planId = subscription?.EffectivePlanId ?? Plan.FreeId;
        return await billing.GetPlanAsync(planId, ct)
               ?? throw new InvalidOperationException($"Formule « {planId} » absente de la table Plans.");
    }

    public async Task<BandUsageDto> GetUsageAsync(Guid bandId, CancellationToken ct)
    {
        var used = await billing.GetStorageUsedAsync(bandId, ct) ?? throw new NotFoundException("Band", bandId);
        var plan = await EffectivePlanAsync(bandId, ct);
        return new BandUsageDto(plan.Id, plan.Name, used, plan.StorageQuotaBytes);
    }

    /// <summary>Refuse (402) un fichier qui ferait dépasser le quota. Rien n'est jamais effacé : on refuse le suivant.</summary>
    public async Task EnsureStorageAsync(Guid bandId, long incomingBytes, CancellationToken ct)
    {
        var used = await billing.GetStorageUsedAsync(bandId, ct) ?? throw new NotFoundException("Band", bandId);
        var plan = await EffectivePlanAsync(bandId, ct);
        if (plan.HasRoomFor(used, incomingBytes)) return;

        throw new PlanLimitException(
            $"Stockage du groupe plein : {ByteSize.Format(used)} utilisés sur {ByteSize.Format(plan.StorageQuotaBytes)} " +
            $"avec la formule {plan.Name}, et ce fichier fait {ByteSize.Format(incomingBytes)}. " +
            "Supprimez des fichiers ou passez à une formule supérieure.");
    }

    /// <summary>Après un envoi ou une suppression : le stockage utilisé suit les fichiers enregistrés.</summary>
    public Task RefreshStorageAsync(Guid bandId, CancellationToken ct) => billing.RefreshStorageUsedAsync(bandId, ct);

    /// <summary>Refuse (402) un groupe de plus quand le compte est déjà Owner d'assez de groupes gratuits.</summary>
    public async Task EnsureCanCreateBandAsync(string userId, CancellationToken ct)
    {
        var free = await billing.GetPlanAsync(Plan.FreeId, ct)
                   ?? throw new InvalidOperationException("Formule gratuite absente de la table Plans.");
        if (free.MaxBandsPerOwner is not { } max) return;
        if (await billing.CountOwnedBandsOnFreePlanAsync(userId, ct) < max) return;

        throw new PlanLimitException(
            $"La formule {free.Name} permet d'être propriétaire de {max} groupe{(max > 1 ? "s" : "")} sans abonnement. " +
            "Passez l'un de vos groupes à une formule payante pour en créer un autre.");
    }
}