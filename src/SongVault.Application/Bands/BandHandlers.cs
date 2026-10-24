using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Bands;
using SongVault.Application.Billing;
using SongVault.Application.Audit;
using SongVault.Domain.Audit;

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