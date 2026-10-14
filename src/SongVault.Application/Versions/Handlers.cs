using SongVault.Application.Abstractions;
using SongVault.Application.Activity;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Activity;
using SongVault.Domain.Songs;

namespace SongVault.Application.Versions;

public sealed class CreateSongVersionHandler(
    ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock, ActivityLog activity)
{
    private const int MaxAttempts = 3;

    public async Task<SongVersionDto> HandleAsync(CreateSongVersionCommand command, CancellationToken ct)
    {
        // Validée UNE fois, avant la boucle : une tonalité invalide ne mérite pas de nouvelle tentative
        var key = MusicalKey.ParseOptional(command.Key);

        for (var attempt = 1; ; attempt++)
        {
            var song = await songs.GetByIdAsync(command.SongId, ct)
                       ?? throw new NotFoundException("Song", command.SongId);

            var now = clock.GetUtcNow();
            var version = song.AddVersion(command.Title, command.Status, command.Notes, command.Lyrics,
                now, command.Bpm, key);
            activity.VersionChanged(ActivityType.VersionAdded, song, version, now);   // oublié aussi par DiscardChanges
            try
            {
                await unitOfWork.SaveChangesAsync(ct);
                return SongVersionDto.From(version);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                unitOfWork.DiscardChanges();
                await Task.Delay(Random.Shared.Next(10, 50) * attempt, ct);
            }
        }
    }
}

public sealed class ListSongVersionsHandler(ISongRepository songs)
{
    public async Task<IReadOnlyList<SongVersionSummaryDto>> HandleAsync(Guid songId, CancellationToken ct)
    {
        if (!await songs.ExistsAsync(songId, ct)) throw new NotFoundException("Song", songId);
        return await songs.ListVersionsAsync(songId, ct);
    }
}

public sealed class GetSongVersionHandler(ISongRepository songs)
{
    public async Task<SongVersionDto> HandleAsync(Guid songId, Guid versionId, CancellationToken ct)
        => await songs.GetVersionAsync(songId, versionId, ct)
           ?? throw new NotFoundException("SongVersion", versionId);
}

public sealed class UpdateSongVersionHandler(
    ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock, ActivityLog activity)
{
    public async Task HandleAsync(UpdateSongVersionCommand command, CancellationToken ct)
    {
        var key = MusicalKey.ParseOptional(command.Key);

        var song = await songs.GetWithVersionAsync(command.SongId, command.VersionId, ct)
                   ?? throw new NotFoundException("Song", command.SongId);
        var version = song.FindVersion(command.VersionId)
                      ?? throw new NotFoundException("SongVersion", command.VersionId);

        var now = clock.GetUtcNow();
        version.Update(command.Title, command.Status, command.Notes, command.Lyrics, now, command.Bpm, key);
        activity.VersionChanged(ActivityType.VersionUpdated, song, version, now);
        await unitOfWork.SaveChangesAsync(ct);
    }
}