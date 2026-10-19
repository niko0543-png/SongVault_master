using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Common;
using SongVault.Domain.Setlists;

namespace SongVault.Application.Setlists;

// Lire = tout membre ; écrire = Member ou Owner (ADR 0008, appliqué par [BandScoped] sur le contrôleur).

public sealed class ListSetlistsHandler(ISetlistRepository setlists)
{
    public Task<IReadOnlyList<SetlistSummaryDto>> HandleAsync(CancellationToken ct) => setlists.ListAsync(ct);
}

public sealed class GetSetlistHandler(ISetlistRepository setlists)
{
    public async Task<SetlistDto> HandleAsync(Guid id, CancellationToken ct)
        => await setlists.GetDetailAsync(id, ct) ?? throw new NotFoundException("Setlist", id);
}

public sealed class CreateSetlistHandler(
    ISetlistRepository setlists, IUnitOfWork unitOfWork, IBandContext band, TimeProvider clock)
{
    public async Task<SetlistDto> HandleAsync(CreateSetlistCommand command, CancellationToken ct)
    {
        var setlist = Setlist.Create(band.RequiredBandId, command.Name, command.Date, command.Venue, clock.GetUtcNow());
        setlists.Add(setlist);
        await unitOfWork.SaveChangesAsync(ct);
        return SetlistDto.FromNew(setlist);
    }
}

public sealed class UpdateSetlistHandler(ISetlistRepository setlists, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task HandleAsync(UpdateSetlistCommand command, CancellationToken ct)
    {
        var setlist = await setlists.GetRequiredAsync(command.Id, ct);
        setlist.UpdateDetails(command.Name, command.Date, command.Venue, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class DeleteSetlistHandler(ISetlistRepository setlists, IUnitOfWork unitOfWork)
{
    /// <summary>Supprime la setlist et ses lignes. Les morceaux et leurs versions restent.</summary>
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        setlists.Remove(await setlists.GetRequiredAsync(id, ct));
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class SetSetlistItemsHandler(
    ISetlistRepository setlists, ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock)
{
    /// <summary>
    /// Remplace les lignes par la liste reçue, dans son ordre. Chaque morceau doit être dans le groupe de la setlist
    /// (hors corbeille), et chaque version donnée doit appartenir à son morceau.
    /// </summary>
    public async Task<SetlistDto> HandleAsync(SetSetlistItemsCommand command, CancellationToken ct)
    {
        var setlist = await setlists.GetRequiredAsync(command.SetlistId, ct);
        Setlist.EnsureValidItems(command.Items);

        // Lectures limitées au groupe actif : un morceau d'un autre groupe, ou à la corbeille, n'y figure pas
        var known = await songs.ListExistingIdsAsync([.. command.Items.Select(i => i.SongId)], ct);
        if (command.Items.Any(i => !known.Contains(i.SongId)))
            throw new DomainException("Chaque morceau doit appartenir au groupe de la setlist.");
        var chosen = command.Items.Where(i => i.VersionId is not null).ToList();
        var songOfVersion = await songs.GetVersionSongIdsAsync([.. chosen.Select(i => i.VersionId!.Value)], ct);
        foreach (var item in chosen)
            if (!songOfVersion.TryGetValue(item.VersionId!.Value, out var songId) || songId != item.SongId)
                throw new DomainException("Chaque version doit appartenir à son morceau.");

        // Deux passes dans une transaction, comme les pistes d'une sortie (ADR 0016) : l'index unique
        // (SetlistId, Position) est vérifié ligne à ligne, un échange 1 ↔ 2 en une seule passe le violerait.
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            setlist.ParkPositions();
            await unitOfWork.SaveChangesAsync(token);
            setlist.SetItems(command.Items, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(token);
        }, ct);

        return await setlists.GetDetailAsync(setlist.Id, ct) ?? throw new NotFoundException("Setlist", setlist.Id);
    }
}

internal static class SetlistLookups
{
    public static async Task<Setlist> GetRequiredAsync(this ISetlistRepository setlists, Guid id, CancellationToken ct)
        => await setlists.GetAsync(id, ct) ?? throw new NotFoundException("Setlist", id);
}