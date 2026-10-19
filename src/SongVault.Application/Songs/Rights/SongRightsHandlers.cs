using SongVault.Application.Abstractions;
using SongVault.Application.Audit;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files;
using SongVault.Domain.Audit;
using SongVault.Domain.Songs;

namespace SongVault.Application.Songs.Rights;

// Lire et exporter = tout membre ; écrire = Member ou Owner (ADR 0008, appliqué par [BandScoped] sur le contrôleur).

public sealed class GetSongRightsHandler(ISongRepository songs)
{
    public async Task<SongRightsDto> HandleAsync(Guid songId, CancellationToken ct)
        => SongRightsDto.From(await songs.GetRequiredWithRightsAsync(songId, ct));
}

public sealed class SetSongRightsHandler(
    ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock, AuditLog audit)
{
    /// <summary>Remplace l'ISWC et les crédits ; la règle des 100 % est vérifiée par Song.SetRights (ADR 0029).</summary>
    public async Task<SongRightsDto> HandleAsync(SetSongRightsCommand command, CancellationToken ct)
    {
        var song = await songs.GetRequiredWithRightsAsync(command.SongId, ct);
        var iswc = Iswc.ParseOptional(command.Iswc);
        var now = clock.GetUtcNow();

        song.SetRights(iswc, command.Credits, now);
        audit.Record(song.BandId, AuditAction.CreditsChanged, song.Title, now, details: Summary(song));   // #19
        await unitOfWork.SaveChangesAsync(ct);
        return SongRightsDto.From(song);
    }

    /// <summary>« 3 crédits, ISWC T-034.524.680-1 » : de quoi voir dans le journal qu'on a touché aux parts.</summary>
    internal static string Summary(Song song)
    {
        var count = song.Credits.Count;
        var credits = count switch { 0 => "aucun crédit", 1 => "1 crédit", _ => $"{count} crédits" };
        return song.Iswc is { } iswc ? $"{credits}, ISWC {iswc.Value}" : $"{credits}, sans ISWC";
    }
}

public sealed class SetVersionIsrcHandler(ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<VersionIsrcDto> HandleAsync(SetVersionIsrcCommand command, CancellationToken ct)
    {
        var song = await songs.GetWithVersionAsync(command.SongId, command.VersionId, ct)
                   ?? throw new NotFoundException("Song", command.SongId);
        var version = song.FindVersion(command.VersionId)
                      ?? throw new NotFoundException("SongVersion", command.VersionId);

        version.SetIsrc(Isrc.ParseOptional(command.Isrc), clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        return VersionIsrcDto.From(version);
    }
}

public sealed class ExportSongCreditsHandler(ISongRepository songs)
{
    public async Task<FileDownload> HandleAsync(Guid songId, CancellationToken ct)
    {
        var song = await songs.GetRequiredWithRightsAsync(songId, ct);
        return CreditsCsv.File($"credits-{song.Title}", CreditsCsv.ForSong(song));
    }
}

public sealed class ExportReleaseCreditsHandler(IReleaseRepository releases, ISongRepository songs)
{
    /// <summary>Une ligne par ayant droit de chaque piste, dans l'ordre des pistes (#22 ; le kit de sortie #24 la reprendra).</summary>
    public async Task<FileDownload> HandleAsync(Guid releaseId, CancellationToken ct)
    {
        var release = await releases.GetDetailAsync(releaseId, ct) ?? throw new NotFoundException("Release", releaseId);
        var withRights = await songs.ListWithRightsAsync([.. release.Tracks.Select(t => t.SongId).Distinct()], ct);
        return CreditsCsv.File($"credits-{release.Title}", CreditsCsv.ForRelease(release, withRights));
    }
}

internal static class SongRightsLookups
{
    public static async Task<Song> GetRequiredWithRightsAsync(this ISongRepository songs, Guid songId, CancellationToken ct)
        => await songs.GetWithRightsAsync(songId, ct) ?? throw new NotFoundException("Song", songId);
}