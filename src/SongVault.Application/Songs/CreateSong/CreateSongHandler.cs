using SongVault.Application.Abstractions;
using SongVault.Domain.Songs;

namespace SongVault.Application.Songs.CreateSong;

public sealed class CreateSongHandler(
    ISongRepository songs, IUnitOfWork unitOfWork, IBandContext bandContext, TimeProvider clock)
{
    public async Task<SongDto> HandleAsync(CreateSongCommand command, CancellationToken ct)
    {
        var song = Song.Create(bandContext.RequiredBandId, command.Title, command.Artist, command.Description, clock.GetUtcNow());
        songs.Add(song);
        await unitOfWork.SaveChangesAsync(ct);
        return SongDto.From(song);
    }
}