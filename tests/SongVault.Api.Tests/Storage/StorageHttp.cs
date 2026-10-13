using SongVault.Application.Abstractions;

namespace SongVault.Api.Tests.Storage;

/// <summary>Appels directs au stockage, hors de l'API, comme le navigateur : aucun cookie.</summary>
internal static class StorageHttp
{
    public static readonly HttpClient Client = new();

    public static async Task<HttpResponseMessage> PutAsync(DirectUpload upload, byte[] bytes)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, upload.Url) { Content = new ByteArrayContent(bytes) };
        foreach (var (name, value) in upload.Headers) request.Headers.Add(name, value);
        return await Client.SendAsync(request);
    }
}