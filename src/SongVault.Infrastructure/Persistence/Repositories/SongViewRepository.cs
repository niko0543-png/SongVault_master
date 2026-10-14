using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Activity;

namespace SongVault.Infrastructure.Persistence.Repositories;

internal sealed class SongViewRepository(SongVaultDbContext db) : ISongViewRepository
{
    public async Task MarkSeenAsync(Guid songId, string userId, DateTimeOffset now, CancellationToken ct)
    {
        // Déjà ouvert : une seule requête UPDATE
        var updated = await db.SongViews
            .Where(v => v.UserId == userId && v.SongId == songId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.SeenAt, now), ct);
        if (updated > 0) return;

        // Première ouverture. Si un autre onglet vient d'insérer la même ligne, la clé primaire refuse
        // la seconde (2627, traduit en 409 par SaveChangesAsync) : l'ouverture est déjà notée, rien à faire.
        var view = SongView.Create(userId, songId, now);
        db.SongViews.Add(view);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            db.Entry(view).State = EntityState.Detached;
        }
    }
}