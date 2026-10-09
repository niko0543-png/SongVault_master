using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Bands;

namespace SongVault.Application.Bands;

public sealed class ListMembersHandler(IBandRepository bands, IBandContext bandContext)
{
    /// <summary>Membres du groupe actif, visibles par tout membre.</summary>
    public Task<IReadOnlyList<BandMemberDto>> HandleAsync(CancellationToken ct)
        => bands.ListMembersAsync(bandContext.RequiredBandId, ct);
}

public sealed class ChangeMemberRoleHandler(IBandRepository bands, IUnitOfWork unitOfWork, IBandContext bandContext)
{
    /// <summary>Change le rôle d'un membre du groupe actif (appelant Owner, vérifié par [MinimumBandRole]).</summary>
    public async Task HandleAsync(string userId, BandRole role, CancellationToken ct)
    {
        var band = await bands.GetWithMemberAsync(bandContext.RequiredBandId, userId, ct);
        band.ChangeRole(userId, role);           // LastOwnerException → 409
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class RemoveMemberHandler(IBandRepository bands, IUnitOfWork unitOfWork, IBandContext bandContext)
{
    /// <summary>Retire un membre du groupe actif (appelant Owner, vérifié par [MinimumBandRole]).</summary>
    public async Task HandleAsync(string userId, CancellationToken ct)
    {
        var band = await bands.GetWithMemberAsync(bandContext.RequiredBandId, userId, ct);
        band.RemoveMember(userId);               // LastOwnerException → 409
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class LeaveBandHandler(
    IBandRepository bands, IUnitOfWork unitOfWork, IBandContext bandContext, ICurrentUser currentUser)
{
    /// <summary>L'utilisateur courant quitte le groupe actif. Refusé (409) s'il en est le dernier Owner.</summary>
    public async Task HandleAsync(CancellationToken ct)
    {
        var band = await bands.GetWithMemberAsync(bandContext.RequiredBandId, currentUser.UserId, ct);
        band.RemoveMember(currentUser.UserId);
        await unitOfWork.SaveChangesAsync(ct);
    }
}

internal static class BandRepositoryExtensions
{
    /// <summary>Groupe avec ses adhésions ; 404 si le groupe n'existe pas ou si userId n'en est pas membre.</summary>
    public static async Task<Band> GetWithMemberAsync(
        this IBandRepository bands, Guid bandId, string userId, CancellationToken ct)
    {
        var band = await bands.GetWithMembersAsync(bandId, ct) ?? throw new NotFoundException("Band", bandId);
        return band.HasMember(userId) ? band : throw new NotFoundException("BandMember", userId);
    }
}