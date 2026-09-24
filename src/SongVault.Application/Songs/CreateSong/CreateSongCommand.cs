namespace SongVault.Application.Songs.CreateSong;

public sealed record CreateSongCommand(string Title, string? Artist, string? Description);