using System.IO.Compression;
using System.Text;

using SongVault.Application.Accounts;
using SongVault.Domain.Bands;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Accounts;

public sealed class AccountExportWriterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task L_archive_contient_data_json_et_les_fichiers_sans_cle_de_stockage()
    {
        var fileId = Guid.NewGuid();
        var file = new AccountFileData(fileId, "maquette.mp3", "audio/mpeg", 3, Now, "secret-key.bin");
        var version = new AccountVersionData(Guid.NewGuid(), 1, "v1", SongVersionStatus.Demo, 120, "F#m", null, "La la",
            Now, Now, [file]);
        var song = new AccountSongData(Guid.NewGuid(), "Export", null, null, Now, Now, [version]);
        var data = new AccountData("u1", "ana@test.local", true,
            [new AccountBandData(Guid.NewGuid(), "Les Rats", BandRole.Owner, Now, [song])], [], []);
        using var output = new MemoryStream();

        await AccountExportWriter.WriteAsync(output, data, Now,
            (key, _) => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes($"contenu de {key}"))),
            CancellationToken.None);

        using var zip = new ZipArchive(new MemoryStream(output.ToArray()));
        var json = await new StreamReader(zip.GetEntry("data.json")!.Open()).ReadToEndAsync();
        Assert.Contains("\"email\": \"ana@test.local\"", json);
        Assert.Contains("\"status\": \"Demo\"", json);
        Assert.Contains($"\"path\": \"files/{fileId}/maquette.mp3\"", json);
        Assert.DoesNotContain("secret-key", json);

        var content = await new StreamReader(zip.GetEntry($"files/{fileId}/maquette.mp3")!.Open()).ReadToEndAsync();
        Assert.Equal("contenu de secret-key.bin", content);
    }
}