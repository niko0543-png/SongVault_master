using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace SongVault.Api.Tests;

internal static class TestFiles
{
    /// <summary>Octets aléatoires précédés de "ID3" : un MP3 du point de vue de la signature.</summary>
    public static byte[] Mp3(int size)
    {
        var bytes = RandomNumberGenerator.GetBytes(size);
        "ID3"u8.CopyTo(bytes);
        return bytes;
    }

    public static MultipartFormDataContent Form(byte[] bytes, string fileName)
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { part, "file", fileName } };
    }
}