using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;

namespace SongVault.Application.Versions;

public sealed class CreateSongVersionHandler(ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock)
{
    private const int MaxAttempts = 3;

    public async Task<SongVersionDto> HandleAsync(CreateSongVersionCommand command, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            // Relu à CHAQUE tentative : LastVersionNumber et RowVersion à jour
            var song = await songs.GetByIdAsync(command.SongId, ct)
                       ?? throw new NotFoundException("Song", command.SongId);

            var version = song.AddVersion(command.Title, command.Status, command.Notes, command.Lyrics, clock.GetUtcNow());
            try
            {
                await unitOfWork.SaveChangesAsync(ct);
                return SongVersionDto.From(version);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                unitOfWork.DiscardChanges();                                        // sinon EF relit l'entité en cache
                await Task.Delay(Random.Shared.Next(10, 50) * attempt, ct);         // petite attente aléatoire
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

public sealed class UpdateSongVersionHandler(ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task HandleAsync(UpdateSongVersionCommand command, CancellationToken ct)
    {
        var song = await songs.GetWithVersionAsync(command.SongId, command.VersionId, ct)
                   ?? throw new NotFoundException("Song", command.SongId);
        var version = song.FindVersion(command.VersionId)
                      ?? throw new NotFoundException("SongVersion", command.VersionId);

        version.Update(command.Title, command.Status, command.Notes, command.Lyrics, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
    }
}