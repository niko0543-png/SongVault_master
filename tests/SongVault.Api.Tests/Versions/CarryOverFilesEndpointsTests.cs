using System.Net;
using System.Net.Http.Json;

using SongVault.Api.Contracts.Files;
using SongVault.Api.Contracts.Versions;
using SongVault.Api.Tests.Files;
using SongVault.Domain.Files;

namespace SongVault.Api.Tests.Versions;

/// <summary>ADR 0046 : une nouvelle version reprend des fichiers d'une version précédente, contre SQL Server.</summary>
[Collection(ApiCollection.Name)]
public sealed class CarryOverFilesEndpointsTests(SongVaultApiFactory factory)
{
    private static async Task<SongFileResponse> UploadAsync(HttpClient client, string versionUrl, byte[] bytes, string fileName)
    {
        var response = await client.PostAsync($"{versionUrl}/files", TestFiles.Form(bytes, fileName));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!;
    }

    // …/songs/{songId}/versions/{versionId} → (…/songs/{songId}/versions, versionId)
    private static (string VersionsUrl, Guid VersionId) Split(string versionUrl)
    {
        var slash = versionUrl.LastIndexOf('/');
        return (versionUrl[..slash], Guid.Parse(versionUrl[(slash + 1)..]));
    }

    [Fact]
    public async Task Les_fichiers_choisis_sont_copies_et_lisibles_une_fois_analyses()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var v1Url = await owner.CreateVersionUrlAsync("Nocturne");
        var mp3 = TestFiles.Mp3(2048);
        var mix = await UploadAsync(owner, v1Url, mp3, "mix.mp3");
        await UploadAsync(owner, v1Url, "Paroles du refrain"u8.ToArray(), "paroles.txt");
        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);
        var (versionsUrl, v1Id) = Split(v1Url);

        var response = await owner.PostAsJsonAsync(versionsUrl,
            new { title = "v2", status = "Demo", lyrics = "Refrain", copyFromVersionId = v1Id, copyFileIds = new[] { mix.Id } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var v2 = (await response.Content.ReadFromJsonAsync<SongVersionResponse>(TestJson.Options))!;
        Assert.Equal(2, v2.Number);
        Assert.Equal("Refrain", v2.Lyrics);
        var copy = Assert.Single(v2.Files);
        Assert.Equal(("mix.mp3", 2048L, FileScanStatus.Pending), (copy.OriginalFileName, copy.SizeBytes, copy.Scan));
        Assert.NotEqual(mix.Id, copy.Id);

        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);           // analyse antivirus de la copie

        Assert.Equal(mp3, await owner.GetByteArrayAsync($"{versionsUrl}/{v2.Id}/files/{copy.Id}/content"));
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"{versionsUrl}/{v2.Id}/files/{copy.Id}")).StatusCode);
        Assert.Equal(mp3, await owner.GetByteArrayAsync($"{v1Url}/files/{mix.Id}/content"));   // l'original reste
    }

    [Fact]
    public async Task Un_fichier_d_une_autre_version_ou_inconnu_est_refuse_sans_creer_de_version()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var v1Url = await owner.CreateVersionUrlAsync("Nocturne");
        var (versionsUrl, v1Id) = Split(v1Url);

        var unknownFile = await owner.PostAsJsonAsync(versionsUrl,
            new { title = "v2", status = "Demo", copyFromVersionId = v1Id, copyFileIds = new[] { Guid.NewGuid() } });
        var noSource = await owner.PostAsJsonAsync(versionsUrl,
            new { title = "v2", status = "Demo", copyFileIds = new[] { Guid.NewGuid() } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknownFile.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noSource.StatusCode);
        Assert.Single((await owner.GetFromJsonAsync<List<SongVersionSummaryResponse>>(versionsUrl, TestJson.Options))!);
    }
}
