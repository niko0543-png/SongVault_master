using System.Net.Http.Json;

using SongVault.Api.Contracts.Files;
using SongVault.Api.Contracts.Songs;

namespace SongVault.Api.Tests.Accounts;

internal static class AccountTestData
{
    /// <summary>Crée, dans le groupe personnel du client, un morceau avec une version et un MP3.</summary>
    public static async Task<(Guid SongId, Guid FileId)> CreateSongWithFileAsync(
        this HttpClient client, string title, byte[] mp3)
    {
        var songs = await client.SongsUrlAsync();
        var song = (await (await client.PostAsJsonAsync(songs, new { title }))
            .Content.ReadFromJsonAsync<SongResponse>(TestJson.Options))!;
        var version = await client.PostAsJsonAsync($"{songs}/{song.Id}/versions", new { title = "v1", status = "Demo" });
        var upload = await client.PostAsync($"{version.Headers.Location}/files", TestFiles.Form(mp3, "maquette.mp3"));
        upload.EnsureSuccessStatusCode();
        var file = (await upload.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!;
        return (song.Id, file.Id);
    }
}