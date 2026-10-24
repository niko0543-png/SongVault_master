using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Bands;
using SongVault.Application.Billing;
using SongVault.Application.Audit;
using SongVault.Domain.Audit;
using SongVault.Domain.Common;

namespace SongVault.Application.Bands;

public sealed class ListMyBandsHandler(IBandRepository bands, ICurrentUser currentUser)
{
    /// <summary>
    /// Groupes de l'utilisateur, éventuellement aucun : un nouvel inscrit choisit lui-même le nom
    /// de son premier groupe (page d'accueil du front) au lieu d'un « Groupe de … » créé d'office.
    /// </summary>
    public Task<IReadOnlyList<BandSummaryDto>> HandleAsync(CancellationToken ct)
        => bands.ListForUserAsync(currentUser.UserId, ct);
}

public sealed class CreateBandHandler(
    IBandRepository bands, IUnitOfWork unitOfWork, ICurrentUser currentUser, TimeProvider clock, PlanLimits limits)
{
    /// <summary>Crée un groupe dont l'utilisateur courant devient Owner. 402 s'il a déjà assez de groupes gratuits (#16).</summary>
    public async Task<BandSummaryDto> HandleAsync(string name, CancellationToken ct)
    {
        await limits.EnsureCanCreateBandAsync(currentUser.UserId, ct);
        var band = Band.Create(name, currentUser.UserId, clock.GetUtcNow());
        bands.Add(band);
        await unitOfWork.SaveChangesAsync(ct);
        return new BandSummaryDto(band.Id, band.Name, BandRole.Owner);
    }
}

public sealed class RenameBandHandler(
    IBandRepository bands, IUnitOfWork unitOfWork, IBandContext bandContext, AuditLog audit, TimeProvider clock)
{
    /// <summary>Renomme le groupe actif (adhésion déjà vérifiée par [BandScoped]).</summary>
    public async Task HandleAsync(string name, CancellationToken ct)
    {
        var band = await bands.GetByIdAsync(bandContext.RequiredBandId, ct)
                   ?? throw new NotFoundException("Band", bandContext.RequiredBandId);
        var previous = band.Name;
        band.Rename(name);
        if (band.Name != previous)
            audit.Record(band.Id, AuditAction.BandRenamed, band.Name, clock.GetUtcNow(), details: $"ancien nom : {previous}");
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class DeleteBandHandler(
    IBandRepository bands, IBandContext bandContext, IBillingRepository billing, IPaymentGateway payments,
    IFileStorageService storage)
{
    /// <summary>
    /// Efface tout de suite le groupe actif et tout son contenu (ADR 0048). Réservé aux Owner ([MinimumBandRole]).
    /// 422 si le nom recopié ne correspond pas, 409 si le groupe a un abonnement Stripe qui n'est pas résilié :
    /// il serait encore prélevé. Un abonnement résilié pour la fin de la période s'arrête tout de suite, avec le groupe.
    /// </summary>
    public async Task HandleAsync(string confirmName, CancellationToken ct)
    {
        var bandId = bandContext.RequiredBandId;
        var band = await bands.GetByIdAsync(bandId, ct) ?? throw new NotFoundException("Band", bandId);
        if (!string.Equals(confirmName.Trim(), band.Name, StringComparison.Ordinal))
            throw new DomainException("Recopiez exactement le nom du groupe pour le supprimer.");

        var subscription = await billing.GetSubscriptionAsync(bandId, ct);
        if (subscription is { IsLiveOnStripe: true, CancelAtPeriodEnd: false })
            throw new ConflictException(
                "Ce groupe a un abonnement en cours : résiliez-le depuis la page Facturation, puis supprimez le groupe.");
        // Avant la base, comme la purge d'un compte : une erreur de Stripe laisse le groupe intact
        if (subscription is { IsLiveOnStripe: true, StripeSubscriptionId: { } subscriptionId })
            await payments.CancelSubscriptionAsync(subscriptionId, ct);

        var keys = await bands.PurgeAsync(bandId, ct);           // la base d'abord, en une transaction

        // Le groupe est déjà effacé : un fichier qui résiste reste orphelin, scripts/purge-orphan-files.ps1 le retrouve
        foreach (var key in keys)
        {
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception)
            {
                // la réponse reste 204 : le groupe n'existe plus pour personne
            }
        }
    }
}
