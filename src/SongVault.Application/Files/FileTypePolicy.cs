using SongVault.Domain.Files;

namespace SongVault.Application.Files;

public sealed record FileTypeRule(string Extension, SongFileType FileType, string ContentType);

public static class FileTypePolicy
{
    public const long MaxFileSizeBytes = 50L * 1024 * 1024;                    // 50 Mo
    public const long MaxRequestSizeBytes = MaxFileSizeBytes + 1024 * 1024;    // + marge pour l'enveloppe multipart

    private static readonly Dictionary<string, FileTypeRule> Rules = new FileTypeRule[]
    {
        new(".mp3",  SongFileType.Audio,     "audio/mpeg"),
        new(".wav",  SongFileType.Audio,     "audio/wav"),
        new(".flac", SongFileType.Audio,     "audio/flac"),
        new(".gp",   SongFileType.Tablature, "application/octet-stream"),
        new(".gp5",  SongFileType.Tablature, "application/octet-stream"),
        new(".gpx",  SongFileType.Tablature, "application/octet-stream"),
        new(".pdf",  SongFileType.Document,  "application/pdf"),
        new(".txt",  SongFileType.Document,  "text/plain; charset=utf-8"),
    }.ToDictionary(r => r.Extension, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> AllowedExtensions => Rules.Keys;

    public static FileTypeRule? Resolve(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.IsNullOrEmpty(extension) ? null : Rules.GetValueOrDefault(extension);
    }
}