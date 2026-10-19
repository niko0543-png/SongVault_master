using SongVault.Application.Abstractions;
using SongVault.Application.Audit;
using SongVault.Application.Billing;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files;
using SongVault.Domain.Audit;
using SongVault.Domain.Trash;

namespace SongVault.Application.Trash;

public sealed class ListTrashHandler(ITrashRepository trash)
{
    public Task<IReadOnlyList<TrashItemDto>> HandleAsync(CancellationToken ct) => trash.ListAsync(ct);
}

/// <summary>Remet en place un élément de la corbeille du groupe actif (Member ou Owner).</summary>
public sealed class RestoreFromTrashHandler(ITrashRepository trash, IUnitOfWork unitOfWork, AuditLog audit, TimeProvider clock)
{
    public async Task HandleAsync(TrashItemKind kind, Guid itemId, CancellationToken ct)
    {
        var song = await trash.GetSongAsync(kind, itemId, ct) ?? throw new NotFoundException(kind.ToString(), itemId);
        var now = clock.GetUtcNow();

        switch (kind)
        {
            case TrashItemKind.Song:
                if (!song.IsDeleted) throw new NotFoundException("Song", itemId);
                song.Restore();
                audit.Record(song.BandId, AuditAction.SongRestored, song.Title, now);
                break;

            case TrashItemKind.Version:
                if (song.IsDeleted)
                    throw new ConflictException($"Le morceau « {song.Title} » est à la corbeille : restaurez-le, il revient avec cette version.");
                var version = song.RestoreVersion(itemId) ?? throw new NotFoundException("SongVersion", itemId);
                audit.Record(song.BandId, AuditAction.VersionRestored, TrashPurger.VersionSubject(song.Title, version.Number), now);
                break;

            case TrashItemKind.File:
                var owner = song.Versions.Single(v => v.Files.Any(f => f.Id == itemId));
                if (song.IsDeleted || owner.IsDeleted)
                    throw new ConflictException(
                        $"La version {owner.Number} de « {song.Title} » est à la corbeille : restaurez-la, elle revient avec ce fichier.");
                var file = owner.RestoreFile(itemId) ?? throw new NotFoundException("SongFile", itemId);
                audit.Record(song.BandId, AuditAction.FileRestored,
                    DeleteSongFileHandler.FileSubject(song.Title, owner.Number, file.OriginalFileName), now);
                break;
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}

/// <summary>Supprime définitivement un élément de la corbeille du groupe actif, sans attendre la purge (Owner).</summary>
public sealed class DeleteFromTrashHandler(ITrashRepository trash, TrashPurger purger, IBandContext band, ICurrentUser user)
{
    public async Task HandleAsync(TrashItemKind kind, Guid itemId, CancellationToken ct)
    {
        var target = await trash.GetPurgeTargetAsync(kind, itemId, ct);
        if (target is null || target.BandId != band.RequiredBandId)            // un autre groupe : introuvable
            throw new NotFoundException(kind.ToString(), itemId);
        await purger.PurgeAsync(kind, itemId, target, user.UserId, ct);
    }
}

/// <summary>Purge quotidienne (#20) : ce qui est à la corbeille depuis 30 jours ou plus part pour de bon.</summary>
public sealed class PurgeExpiredTrashHandler(ITrashRepository trash, TrashPurger purger, TimeProvider clock)
{
    /// <returns>Nombre d'éléments effacés.</returns>
    public async Task<int> HandleAsync(CancellationToken ct)
    {
        var expired = await trash.ListExpiredAsync(clock.GetUtcNow() - TrashPolicy.Retention, ct);
        var purged = 0;
        foreach (var entry in expired)
        {
            // Une version d'un morceau effacé juste avant est déjà partie avec lui
            var target = await trash.GetPurgeTargetAsync(entry.Kind, entry.Id, ct);
            if (target is null) continue;
            await purger.PurgeAsync(entry.Kind, entry.Id, target, actorId: null, ct);
            purged++;
        }
        return purged;
    }
}

/// <summary>
/// Efface un élément de la corbeille, dans cet ordre : ses fichiers du stockage, puis ses lignes, puis la jauge du
/// groupe. Interrompue, la purge recommence le lendemain sans dommage : effacer un fichier absent ne fait rien.
/// </summary>
public sealed class TrashPurger(
    ITrashRepository trash, IFileStorageService storage, IAuditRepository audit, IUnitOfWork unitOfWork,
    PlanLimits limits, TimeProvider clock)
{
    public async Task PurgeAsync(TrashItemKind kind, Guid itemId, PurgeTarget target, string? actorId, CancellationToken ct)
    {
        foreach (var key in target.StorageKeys)                                   // 1. stockage
            await storage.DeleteAsync(key, ct);

        await trash.DeleteAsync(kind, itemId, ct);                                // 2. lignes (cascade)

        var action = kind switch
        {
            TrashItemKind.Song => AuditAction.SongPurged,
            TrashItemKind.Version => AuditAction.VersionPurged,
            _ => AuditAction.FilePurged,
        };
        audit.Add(AuditEntry.Record(target.BandId, actorId, action, target.Label, clock.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(ct);

        await limits.RefreshStorageAsync(target.BandId, ct);                      // 3. la place se libère
    }

    /// <summary>« Nocturne — v2 » : la version telle que le journal d'audit la nomme.</summary>
    public static string VersionSubject(string songTitle, int versionNumber) => $"{songTitle} — v{versionNumber}";
}