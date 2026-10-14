namespace SongVault.Application.Releases;

public sealed record CoverImageRule(string Extension, string ContentType);

/// <summary>Pochettes : JPEG, PNG ou WebP de 5 Mo au plus, contenu vérifié par FileSignatureValidator.</summary>
public static class CoverImagePolicy
{
    public const long MaxFileSizeBytes = 5L * 1024 * 1024;                       // 5 Mo
    public const long MaxRequestSizeBytes = MaxFileSizeBytes + 64 * 1024;        // + marge pour l'enveloppe multipart

    private static readonly Dictionary<string, CoverImageRule> Rules = new CoverImageRule[]
    {
        new(".jpg",  "image/jpeg"),
        new(".jpeg", "image/jpeg"),
        new(".png",  "image/png"),
        new(".webp", "image/webp"),
    }.ToDictionary(r => r.Extension, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> AllowedExtensions => Rules.Keys;

    public static CoverImageRule? Resolve(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.IsNullOrEmpty(extension) ? null : Rules.GetValueOrDefault(extension);
    }
}