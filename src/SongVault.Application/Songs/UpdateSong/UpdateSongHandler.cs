using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;

namespace SongVault.Application.Songs.UpdateSong;

public sealed class UpdateSongHandler(ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task HandleAsync(UpdateSongCommand command, CancellationToken ct)
    {
        var song = await songs.GetByIdAsync(command.Id, ct)
                   ?? throw new NotFoundException("Song", command.Id);

        song.UpdateDetails(command.Title, command.Artist, command.Description, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
    }
}