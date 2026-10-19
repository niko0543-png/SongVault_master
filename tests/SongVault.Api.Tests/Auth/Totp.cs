using System.Buffers.Binary;
using System.Security.Cryptography;

namespace SongVault.Api.Tests.Auth;

/// <summary>Ce que calcule l'application d'authentification (RFC 6238 : HMAC-SHA1, 30 secondes, 6 chiffres).</summary>
public static class Totp
{
    public static string Code(string sharedKey, DateTimeOffset? at = null)
    {
        var key = Base32Decode(sharedKey.Replace(" ", ""));
        var step = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / 30;
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);

        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var value = (BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset, 4)) & 0x7FFFFFFF) % 1_000_000;
        return value.ToString("D6");
    }

    private static byte[] Base32Decode(string text)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in text.TrimEnd('=').ToUpperInvariant())
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }
        return [.. bytes];
    }
}