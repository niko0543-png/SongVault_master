namespace SongVault.Application.Songs.UpdateSong;

public sealed record UpdateSongCommand(Guid Id, string Title, string? Artist, string? Description);