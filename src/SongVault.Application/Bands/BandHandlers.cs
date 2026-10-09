using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Bands;

namespace SongVault.Application.Bands;

public sealed class ListMyBandsHandler(
    IBandRepository bands, IUnitOfWork unitOfWork, ICurrentUser currentUser, TimeProvider clock)
{
    /// <summary>Groupes de l'utilisateur ; crée son groupe personnel s'il n'en a aucun (nouvel inscrit).</summary>
    public async Task<IReadOnlyList<BandSummaryDto>> HandleAsync(CancellationToken ct)
    {
        var list = await bands.ListForUserAsync(currentUser.UserId, ct);
        if (list.Count > 0) return list;

        var localPart = currentUser.Email?.Split('@')[0];
        var name = string.IsNullOrWhiteSpace(localPart) ? "Mon groupe" : $"Groupe de {localPart}";
        bands.Add(Band.Create(name, currentUser.UserId, clock.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(ct);
        return await bands.ListForUserAsync(currentUser.UserId, ct);
    }
}

public sealed class CreateBandHandler(
    IBandRepository bands, IUnitOfWork unitOfWork, ICurrentUser currentUser, TimeProvider clock)
{
    /// <summary>Crée un groupe dont l'utilisateur courant devient Owner.</summary>
    public async Task<BandSummaryDto> HandleAsync(string name, CancellationToken ct)
    {
        var band = Band.Create(name, currentUser.UserId, clock.GetUtcNow());
        bands.Add(band);
        await unitOfWork.SaveChangesAsync(ct);
        return new BandSummaryDto(band.Id, band.Name, BandRole.Owner);
    }
}

public sealed class RenameBandHandler(IBandRepository bands, IUnitOfWork unitOfWork, IBandContext bandContext)
{
    /// <summary>Renomme le groupe actif (adhésion déjà vérifiée par [BandScoped]).</summary>
    public async Task HandleAsync(string name, CancellationToken ct)
    {
        var band = await bands.GetByIdAsync(bandContext.RequiredBandId, ct)
                   ?? throw new NotFoundException("Band", bandContext.RequiredBandId);
        band.Rename(name);
        await unitOfWork.SaveChangesAsync(ct);
    }
}