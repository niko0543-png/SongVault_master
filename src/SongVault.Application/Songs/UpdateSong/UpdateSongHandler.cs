using SongVault.Application.Abstractions;
using SongVault.Application.Activity;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Activity;

namespace SongVault.Application.Songs.UpdateSong;

public sealed class UpdateSongHandler(
    ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock, ActivityLog activity)
{
    public async Task HandleAsync(UpdateSongCommand command, CancellationToken ct)
    {
        var song = await songs.GetByIdAsync(command.Id, ct)
                   ?? throw new NotFoundException("Song", command.Id);

        var now = clock.GetUtcNow();
        song.UpdateDetails(command.Title, command.Artist, command.Description, now);
        activity.SongChanged(ActivityType.SongUpdated, song, now);
        await unitOfWork.SaveChangesAsync(ct);
    }
}