using System.Net;
using System.Net.Http.Json;

using SongVault.Api.Contracts.Common;
using SongVault.Api.Contracts.Files;
using SongVault.Api.Contracts.Songs;
using SongVault.Api.Contracts.Versions;
using SongVault.Api.Tests.Files;

namespace SongVault.Api.Tests.Songs;

/// <summary>#34 : la liste des morceaux et celle des versions disent quelle tablature ouvrir, une fois déclarée saine.</summary>
[Collection(ApiCollection.Name)]
public sealed class TablatureShortcutTests(SongVaultApiFactory factory)
{
    // En-tête d'un fichier Guitar Pro 5 (#28) : le contrôle de signature ne lit que le début
    private static readonly byte[] Gp5 = [.. "\u0018FICHIER GUITAR PRO v5.00"u8, .. new byte[64]];

    private static async Task<SongFileResponse> UploadAsync(HttpClient client, string versionUrl, byte[] bytes, string fileName)
    {
        var response = await client.PostAsync($"{versionUrl}/files", TestFiles.Form(bytes, fileName));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!;
    }

    [Fact]
    public async Task La_tablature_lisible_de_la_derniere_version_s_ouvre_depuis_les_listes()
    {
        var client = await factory.CreateNewUserClientAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var versionUrl = await client.CreateVersionUrlAsync($"Tablature {marker}");
        var segments = new Uri(new Uri("http://localhost"), versionUrl).AbsolutePath.Split('/');
        var (songId, versionId) = (Guid.Parse(segments[^3]), Guid.Parse(segments[^1]));
        var songs = await client.SongsUrlAsync();

        async Task<SongResponse> SongAsync() => Assert.Single(
            (await client.GetFromJsonAsync<PagedResponse<SongResponse>>($"{songs}?search={marker}", TestJson.Options))!.Items);
        async Task<SongVersionSummaryResponse> VersionAsync() => Assert.Single(
            (await client.GetFromJsonAsync<List<SongVersionSummaryResponse>>($"{songs}/{songId}/versions", TestJson.Options))!);

        await UploadAsync(client, versionUrl, TestFiles.Wav(1), "maquette.wav");
        var tab = await UploadAsync(client, versionUrl, Gp5, "riff.gp5");

        // Tant que l'antivirus ne l'a pas déclarée saine, la tablature n'est proposée nulle part
        Assert.Null((await SongAsync()).TablatureFileId);
        Assert.Null((await VersionAsync()).TablatureFileId);

        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);

        var song = await SongAsync();
        Assert.Equal((versionId, tab.Id), (song.TablatureVersionId, song.TablatureFileId));
        Assert.Equal(tab.Id, (await VersionAsync()).TablatureFileId);
    }
}
