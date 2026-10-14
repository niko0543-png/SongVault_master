using SongVault.Application.Abstractions;
using SongVault.Application.Activity;
using SongVault.Domain.Activity;
using SongVault.Domain.Songs;

namespace SongVault.Application.Songs.CreateSong;

public sealed class CreateSongHandler(
    ISongRepository songs, IUnitOfWork unitOfWork, IBandContext bandContext, TimeProvider clock, ActivityLog activity)
{
    public async Task<SongDto> HandleAsync(CreateSongCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var song = Song.Create(bandContext.RequiredBandId, command.Title, command.Artist, command.Description, now);
        songs.Add(song);
        activity.SongChanged(ActivityType.SongCreated, song, now);
        await unitOfWork.SaveChangesAsync(ct);
        return SongDto.From(song);
    }
}