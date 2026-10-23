using SongVault.Application.Abstractions;
using SongVault.Application.Audit;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Trash;
using SongVault.Domain.Audit;
using SongVault.Domain.Bands;
using SongVault.Domain.Common;
using SongVault.Domain.Releases;
using SongVault.Domain.Trash;

namespace SongVault.Application.Releases;

/// <summary>Ce qu'on garde d'un morceau qui sort (#38) : toutes ses versions, ou seulement celle qui sort.</summary>
public enum KeepVersions { All, LatestOnly }

/// <summary>
/// Les infos de sortie de la dernière version d'un morceau (#38, ADR 0044). ReleaseId null : un single à son titre est
/// créé ; sinon le morceau rejoint cette sortie (EP, album). Date, label, code-barres et liens vont sur la sortie.
/// PurgeNow : les versions écartées sont effacées tout de suite au lieu d'attendre 30 jours à la corbeille (Owner).
/// </summary>
public sealed record ReleaseSongCommand(
    Guid SongId, Guid VersionId, Guid? ReleaseId, DateOnly? ReleaseDate, string? Label, string? Upc,
    IReadOnlyList<string> StreamingLinks, KeepVersions Keep, bool PurgeNow);

/// <summary>Une version qui n'a pas pu partir, et pourquoi : les sorties et les setlists qui l'utilisent encore.</summary>
public sealed record KeptVersionDto(int Number, IReadOnlyList<string> Releases, IReadOnlyList<string> Setlists);

/// <summary>La sortie du morceau, les versions écartées (numéros) et celles qui restent malgré « seulement la dernière ».</summary>
public sealed record SongReleaseResultDto(
    Guid ReleaseId, IReadOnlyList<int> RemovedVersions, IReadOnlyList<KeptVersionDto> KeptVersions, bool Purged);

public sealed class ReleaseSongHandler(
    ISongRepository songs, IReleaseRepository releases, ISetlistRepository setlists, ITrashRepository trash,
    TrashPurger purger, IUnitOfWork unitOfWork, IBandContext band, ICurrentUser user, AuditLog audit, TimeProvider clock)
{
    public async Task<SongReleaseResultDto> HandleAsync(ReleaseSongCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.PurgeNow && command.Keep == KeepVersions.All)
            throw new DomainException("Rien à effacer : vous gardez toutes les versions.");
        if (command.PurgeNow && band.Role != BandRole.Owner)
            throw new ForbiddenException("Seul un propriétaire du groupe peut effacer des versions sans passer par la corbeille.");

        var song = await songs.GetWithAllVersionsAsync(command.SongId, ct) ?? throw new NotFoundException("Song", command.SongId);
        if (song.IsReleased)
            throw new ConflictException($"« {song.Title} » est déjà sorti. Remettez-le dans les morceaux pour changer sa sortie.");
        var version = song.EnsureReleasable(command.VersionId);
        var upc = Upc.ParseOptional(command.Upc);                     // tout est vérifié avant la moindre écriture
        var now = clock.GetUtcNow();

        Release release;
        if (command.ReleaseId is { } releaseId)
        {
            release = await releases.GetAsync(releaseId, ct) ?? throw new NotFoundException("Release", releaseId);
            release.UpdateDetails(release.Title, release.Type, command.ReleaseDate ?? release.ReleaseDate, now);
        }
        else
        {
            release = Release.Create(band.RequiredBandId, song.Title, ReleaseType.Single, command.ReleaseDate, now);
            releases.Add(release);
        }
        release.SetDistribution(command.Label, upc, command.StreamingLinks, now);
        release.PutTrack(song.Id, version.Id, now);
        song.MarkReleased(release.Id, now);

        var removed = new List<(Guid Id, int Number)>();
        var kept = new List<KeptVersionDto>();
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            // 1. La sortie et sa piste d'abord : une piste qui passait par une ancienne version la quitte
            await unitOfWork.SaveChangesAsync(token);

            // 2. Les autres versions, sauf celles qu'une autre sortie ou une setlist utilise encore
            if (command.Keep == KeepVersions.LatestOnly)
            {
                foreach (var other in song.Versions.Where(v => !v.IsDeleted && v.Id != version.Id).OrderBy(v => v.Number).ToList())
                {
                    var onReleases = await releases.ListTitlesWithVersionAsync(other.Id, token);
                    var onSetlists = await setlists.ListNamesWithVersionAsync(other.Id, token);
                    if (onReleases.Count > 0 || onSetlists.Count > 0)
                    {
                        kept.Add(new KeptVersionDto(other.Number, onReleases, onSetlists));
                        continue;
                    }
                    song.TrashVersion(other.Id, user.UserId, now);
                    audit.Record(song.BandId, AuditAction.VersionDeleted, TrashPurger.VersionSubject(song.Title, other.Number), now);
                    removed.Add((other.Id, other.Number));
                }
            }
            audit.Record(song.BandId, AuditAction.SongReleased, song.Title, now, details: Details(release, version.Number, removed.Count, command));
            await unitOfWork.SaveChangesAsync(token);
        }, ct);

        // 3. Après la validation seulement : effacer du stockage ne se rattrape pas si la base revient en arrière
        if (command.PurgeNow)
        {
            foreach (var (id, _) in removed)
            {
                var target = await trash.GetPurgeTargetAsync(TrashItemKind.Version, id, ct);
                if (target is not null) await purger.PurgeAsync(TrashItemKind.Version, id, target, user.UserId, ct);
            }
        }

        return new SongReleaseResultDto(release.Id, [.. removed.Select(r => r.Number)], kept, command.PurgeNow && removed.Count > 0);
    }

    // « v4 sur « Nocturne » ; 3 versions à la corbeille » : ce que le journal d'audit garde de la décision
    private static string Details(Release release, int versionNumber, int removedCount, ReleaseSongCommand command)
    {
        var text = $"v{versionNumber} sur « {release.Title} »";
        if (removedCount == 0) return text;
        var plural = removedCount > 1 ? "s" : "";
        return command.PurgeNow
            ? $"{text} ; {removedCount} version{plural} effacée{plural}"
            : $"{text} ; {removedCount} version{plural} à la corbeille";
    }
}

/// <summary>Remet un morceau sorti dans les morceaux en travail (#38). La sortie et sa piste restent.</summary>
public sealed class UnreleaseSongHandler(ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task HandleAsync(Guid songId, CancellationToken ct)
    {
        var song = await songs.GetByIdAsync(songId, ct) ?? throw new NotFoundException("Song", songId);
        if (song.Unrelease(clock.GetUtcNow()))
            await unitOfWork.SaveChangesAsync(ct);
    }
}

/// <summary>Label, code-barres et liens d'écoute d'une sortie (#38), qui remplacent les anciens.</summary>
public sealed class SetReleaseDistributionHandler(IReleaseRepository releases, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<ReleaseDto> HandleAsync(SetReleaseDistributionCommand command, CancellationToken ct)
    {
        var upc = Upc.ParseOptional(command.Upc);
        var release = await releases.GetAsync(command.ReleaseId, ct) ?? throw new NotFoundException("Release", command.ReleaseId);
        release.SetDistribution(command.Label, upc, command.StreamingLinks, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        return await releases.GetDetailAsync(release.Id, ct) ?? throw new NotFoundException("Release", release.Id);
    }
}
