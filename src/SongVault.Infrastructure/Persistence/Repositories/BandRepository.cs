using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Bands;
using SongVault.Domain.Bands;

namespace SongVault.Infrastructure.Persistence.Repositories;

internal sealed class BandRepository(SongVaultDbContext db) : IBandRepository
{
    public void Add(Band band) => db.Bands.Add(band);   // la membership Owner suit, via Band.Memberships

    public Task<Band?> GetByIdAsync(Guid bandId, CancellationToken ct)
        => db.Bands.FirstOrDefaultAsync(b => b.Id == bandId, ct);

    public Task<BandRole?> GetRoleAsync(Guid bandId, string userId, CancellationToken ct)
        => db.BandMemberships.AsNoTracking()
            .Where(m => m.BandId == bandId && m.UserId == userId)
            .Select(m => (BandRole?)m.Role)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<BandSummaryDto>> ListForUserAsync(string userId, CancellationToken ct)
        => await db.BandMemberships.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(db.Bands, m => m.BandId, b => b.Id, (m, b) => new BandSummaryDto(b.Id, b.Name, m.Role))
            .OrderBy(b => b.Name)
            .ToListAsync(ct);
}