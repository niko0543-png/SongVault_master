using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Invitations;
using SongVault.Domain.Invitations;

namespace SongVault.Infrastructure.Persistence.Repositories;

internal sealed class InvitationRepository(SongVaultDbContext db) : IInvitationRepository
{
    public void Add(Invitation invitation) => db.Invitations.Add(invitation);

    public Task<Invitation?> GetByTokenHashAsync(byte[] tokenHash, CancellationToken ct)
        => db.Invitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);

    public Task<Invitation?> GetInBandAsync(Guid bandId, Guid invitationId, CancellationToken ct)
        => db.Invitations.FirstOrDefaultAsync(i => i.Id == invitationId && i.BandId == bandId, ct);

    public async Task<IReadOnlyList<Invitation>> ListPendingForEmailAsync(
        Guid bandId, string email, DateTimeOffset now, CancellationToken ct)
        => await Pending(bandId, now).Where(i => i.Email == email).ToListAsync(ct);

    public async Task<IReadOnlyList<InvitationDto>> ListPendingAsync(Guid bandId, DateTimeOffset now, CancellationToken ct)
        => await Pending(bandId, now).AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitationDto(i.Id, i.Email, i.Role, i.CreatedAt, i.ExpiresAt))
            .ToListAsync(ct);

    // Même définition que Invitation.StatusAt(now) == Pending, mais traduite en SQL
    private IQueryable<Invitation> Pending(Guid bandId, DateTimeOffset now)
        => db.Invitations.Where(i => i.BandId == bandId
                                     && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now);
}