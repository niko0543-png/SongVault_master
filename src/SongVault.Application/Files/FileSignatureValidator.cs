namespace SongVault.Application.Files;

/// <summary>Vérifie que les premiers octets d'un fichier correspondent à son extension.</summary>
public static class FileSignatureValidator
{
    /// <summary>Nombre d'octets à lire en tête de fichier.</summary>
    public const int HeaderLength = 32;

    public static bool Matches(string extension, ReadOnlySpan<byte> header) => extension.ToLowerInvariant() switch
    {
        ".mp3" => IsMp3(header),
        ".wav" => header.Length >= 12 && header.StartsWith("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WAVE"u8),
        ".flac" => header.StartsWith("fLaC"u8),
        ".pdf" => header.StartsWith("%PDF-"u8),
        ".gp" => header.StartsWith((ReadOnlySpan<byte>)[0x50, 0x4B, 0x03, 0x04]),
        ".gpx" => header.StartsWith("BCFZ"u8) || header.StartsWith("BCFS"u8),
        // #28 : GP3 à GP5 commencent par la longueur de leur en-tête puis « FICHIER GUITAR PRO v3.00 » (v4.06, v5.10…)
        ".gp3" or ".gp4" or ".gp5" => header.Length > 1 && header[1..].StartsWith("FICHIER GUITAR PRO"u8),
        ".txt" => header.Length > 0 && !header.Contains((byte)0),
        // Pochettes des sorties (#10)
        ".jpg" or ".jpeg" => header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]),
        ".png" => header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        ".webp" => header.Length >= 12 && header.StartsWith("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8),
        _ => false,                                   // extension inconnue : refus par défaut
    };

    private static bool IsMp3(ReadOnlySpan<byte> h)
        => h.StartsWith("ID3"u8) || (h.Length >= 2 && h[0] == 0xFF && (h[1] & 0xE0) == 0xE0);
}