using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Bands;
using SongVault.Application.Audit;
using SongVault.Domain.Audit;

namespace SongVault.Application.Bands;

public sealed class ListMembersHandler(IBandRepository bands, IBandContext bandContext)
{
    /// <summary>Membres du groupe actif, visibles par tout membre.</summary>
    public Task<IReadOnlyList<BandMemberDto>> HandleAsync(CancellationToken ct)
        => bands.ListMembersAsync(bandContext.RequiredBandId, ct);
}

public sealed class ChangeMemberRoleHandler(
    IBandRepository bands, IUnitOfWork unitOfWork, IBandContext bandContext, AuditLog audit, TimeProvider clock)
{
    /// <summary>Change le rôle d'un membre du groupe actif (appelant Owner, vérifié par [MinimumBandRole]).</summary>
    public async Task HandleAsync(string userId, BandRole role, CancellationToken ct)
    {
        var band = await bands.GetWithMemberAsync(bandContext.RequiredBandId, userId, ct);
        var previous = band.Memberships.Single(m => m.UserId == userId).Role;
        band.ChangeRole(userId, role);           // LastOwnerException → 409
        if (previous != role)
            audit.Record(band.Id, AuditAction.MemberRoleChanged, null, clock.GetUtcNow(), userId, AuditLog.RoleChange(previous, role));
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class RemoveMemberHandler(
    IBandRepository bands, IUnitOfWork unitOfWork, IBandContext bandContext, AuditLog audit, TimeProvider clock)
{
    /// <summary>Retire un membre du groupe actif (appelant Owner, vérifié par [MinimumBandRole]).</summary>
    public async Task HandleAsync(string userId, CancellationToken ct)
    {
        var band = await bands.GetWithMemberAsync(bandContext.RequiredBandId, userId, ct);
        var role = band.Memberships.Single(m => m.UserId == userId).Role;
        band.RemoveMember(userId);               // LastOwnerException → 409
        audit.Record(band.Id, AuditAction.MemberRemoved, null, clock.GetUtcNow(), userId, role.ToString());
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class LeaveBandHandler(
    IBandRepository bands, IUnitOfWork unitOfWork, IBandContext bandContext, ICurrentUser currentUser,
    AuditLog audit, TimeProvider clock)
{
    /// <summary>L'utilisateur courant quitte le groupe actif. Refusé (409) s'il en est le dernier Owner.</summary>
    public async Task HandleAsync(CancellationToken ct)
    {
        var band = await bands.GetWithMemberAsync(bandContext.RequiredBandId, currentUser.UserId, ct);
        band.RemoveMember(currentUser.UserId);
        audit.Record(band.Id, AuditAction.MemberLeft, null, clock.GetUtcNow());
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