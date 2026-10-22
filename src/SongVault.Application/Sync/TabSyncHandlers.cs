using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Application.Files.Sync;

// Lire = tout membre ; écrire = Member ou Owner (ADR 0008, appliqué par [BandScoped] sur le contrôleur).

public sealed class GetTabSyncHandler(ISongRepository songs)
{
    /// <summary>
    /// Le calage de la tablature, ou null si elle n'est pas calée. Une maquette mise à la corbeille seule rend aussi null :
    /// le calage n'est pas effacé, il revient si on la restaure.
    /// </summary>
    public async Task<TabSyncDto?> HandleAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
    {
        var version = await TabSync.FindVersionAsync(songs, songId, versionId, ct);
        var tablature = TabSync.FindTablature(version, fileId);
        if (tablature.SyncAudioFileId is not { } audioId || tablature.SyncPoints is not { } points) return null;
        var audio = version.Files.SingleOrDefault(f => f.Id == audioId && !f.IsDeleted);
        return audio is null ? null : new TabSyncDto(audio.Id, audio.OriginalFileName, points);
    }
}

public sealed class SetTabSyncHandler(ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock)
{
    /// <summary>Remplace le calage ; les règles (tablature et maquette de la version, points dans l'ordre) sont dans SongVersion.</summary>
    public async Task<TabSyncDto> HandleAsync(SetTabSyncCommand command, CancellationToken ct)
    {
        var version = await TabSync.FindVersionAsync(songs, command.SongId, command.VersionId, ct);
        TabSync.FindTablature(version, command.FileId);

        version.SetTablatureSync(command.FileId, command.AudioFileId, command.Points, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        var audio = version.Files.Single(f => f.Id == command.AudioFileId);
        return new TabSyncDto(audio.Id, audio.OriginalFileName, command.Points);
    }
}

public sealed class DeleteTabSyncHandler(ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock)
{
    /// <summary>Retire le calage ; sans calage, ne fait rien (la tablature se joue de nouveau avec son propre son).</summary>
    public async Task HandleAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
    {
        var version = await TabSync.FindVersionAsync(songs, songId, versionId, ct);
        TabSync.FindTablature(version, fileId);
        if (version.ClearTablatureSync(fileId, clock.GetUtcNow()))
            await unitOfWork.SaveChangesAsync(ct);
    }
}

internal static class TabSync
{
    public static async Task<SongVersion> FindVersionAsync(ISongRepository songs, Guid songId, Guid versionId, CancellationToken ct)
    {
        var song = await songs.GetWithVersionAsync(songId, versionId, ct);
        var version = song?.FindVersion(versionId);
        return version is null || version.IsDeleted ? throw new NotFoundException("SongVersion", versionId) : version;
    }

    /// <summary>Un fichier en place qui n'est pas une tablature n'a pas de calage : 404, comme un fichier inconnu.</summary>
    public static SongFile FindTablature(SongVersion version, Guid fileId)
        => version.Files.SingleOrDefault(f => f.Id == fileId && !f.IsDeleted && f.FileType == SongFileType.Tablature)
           ?? throw new NotFoundException("SongFile", fileId);
}