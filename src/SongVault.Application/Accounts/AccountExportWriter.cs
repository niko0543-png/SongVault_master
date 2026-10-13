using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SongVault.Application.Accounts;

/// <summary>Écrit l'archive d'un compte : data.json, puis chaque fichier sous files/{id}/{nom}.</summary>
public static class AccountExportWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,                                   // lisible dans un éditeur de texte
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,  // accents lisibles tels quels : un fichier, pas une page web
        Converters = { new JsonStringEnumConverter() },        // "Owner", "Demo" : comme l'API
    };

    public static async Task WriteAsync(
        Stream output, AccountData data, DateTimeOffset generatedAt,
        Func<string, CancellationToken, Task<Stream>> openFile, CancellationToken ct)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        await using (var json = zip.CreateEntry("data.json").Open())
            await JsonSerializer.SerializeAsync(json, new { generatedAt, account = data }, Json, ct);

        var files = data.Bands.SelectMany(b => b.Songs).SelectMany(s => s.Versions).SelectMany(v => v.Files);
        foreach (var file in files)
        {
            await using var target = zip.CreateEntry(file.Path, CompressionLevel.NoCompression).Open();
            await using var source = await openFile(file.StorageKey, ct);
            await source.CopyToAsync(target, ct);
        }
    }
}