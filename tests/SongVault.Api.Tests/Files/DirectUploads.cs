using System.Net.Http.Json;

using SongVault.Api.Contracts.Files;
using SongVault.Api.Contracts.Songs;
using SongVault.Api.Tests.Storage;
using SongVault.Application.Abstractions;

namespace SongVault.Api.Tests.Files;

internal static class DirectUploads
{
    /// <summary>Crée un morceau et une version dans le groupe personnel du client ; renvoie l'URL de la version.</summary>
    public static async Task<string> CreateVersionUrlAsync(this HttpClient client, string title = "Envoi direct")
    {
        var songs = await client.SongsUrlAsync();
        var song = (await (await client.PostAsJsonAsync(songs, new { title }))
            .Content.ReadFromJsonAsync<SongResponse>(TestJson.Options))!;
        var version = await client.PostAsJsonAsync($"{songs}/{song.Id}/versions", new { title = "v1", status = "Demo" });
        return version.Headers.Location!.ToString();
    }

    public static Task<HttpResponseMessage> StartAsync(this HttpClient client, string versionUrl, string fileName, long sizeBytes)
        => client.PostAsJsonAsync($"{versionUrl}/files/uploads", new { fileName, sizeBytes });

    public static async Task<FileUploadResponse> StartOkAsync(this HttpClient client, string versionUrl, string fileName, long sizeBytes)
    {
        var response = await client.StartAsync(versionUrl, fileName, sizeBytes);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FileUploadResponse>(TestJson.Options))!;
    }

    /// <summary>PUT à l'adresse reçue : l'API en stockage local, le stockage lui-même sinon (sans cookie).</summary>
    public static Task<HttpResponseMessage> PutAsync(this HttpClient client, FileUploadResponse upload, byte[] bytes)
        => Uri.TryCreate(upload.UploadUrl, UriKind.Absolute, out var direct) && direct.Scheme is "http" or "https"
            ? StorageHttp.PutAsync(new DirectUpload(direct, upload.Headers), bytes)
            : client.PutAsync(upload.UploadUrl, new ByteArrayContent(bytes));

    public static Task<HttpResponseMessage> CompleteAsync(this HttpClient client, string versionUrl, Guid uploadId)
        => client.PostAsync($"{versionUrl}/files/uploads/{uploadId}/complete", null);
}