using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Api.Contracts.Me;
using SongVault.Application.Abstractions;
using SongVault.Domain.Accounts;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests.Accounts;

[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public sealed class DataExportTests(SongVaultApiFactory factory)
{
    private static string NewEmail() => $"export-{Guid.NewGuid():N}@test.local";

    [Fact]
    public async Task L_archive_contient_les_donnees_et_les_fichiers_du_compte()
    {
        var email = NewEmail();
        var client = await factory.CreateNewUserClientAsync(email);
        var mp3 = TestFiles.Mp3(2048);
        var (_, fileId) = await client.CreateSongWithFileAsync("Export", mp3);

        var request = await client.PostAsync("/api/me/export", null);
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);

        var export = await client.GetFromJsonAsync<DataExportResponse>("/api/me/export", TestJson.Options);
        Assert.Equal(DataExportStatus.Ready, export!.Status);
        var download = await client.GetAsync("/api/me/export/download");
        Assert.Equal("application/zip", download.Content.Headers.ContentType?.MediaType);

        using var zip = new ZipArchive(new MemoryStream(await download.Content.ReadAsByteArrayAsync()));
        var json = await new StreamReader(zip.GetEntry("data.json")!.Open()).ReadToEndAsync();
        var account = JsonDocument.Parse(json).RootElement.GetProperty("account");
        Assert.Equal(email, account.GetProperty("email").GetString());
        Assert.Contains("\"title\": \"Export\"", json);
        Assert.DoesNotContain("storageKey", json, StringComparison.OrdinalIgnoreCase);

        using var copy = new MemoryStream();
        await zip.GetEntry($"files/{fileId}/maquette.mp3")!.Open().CopyToAsync(copy);
        Assert.Equal(mp3, copy.ToArray());

        Assert.Equal("Vos données SongVault sont prêtes", factory.Emails.LastTo(email).Subject);
    }

    [Fact]
    public async Task Une_seule_archive_en_preparation_a_la_fois()
    {
        var client = await factory.CreateNewUserClientAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/me/export")).StatusCode);

        await client.PostAsync("/api/me/export", null);
        var second = await client.PostAsync("/api/me/export", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/me/export/download")).StatusCode);
    }

    [Fact]
    public async Task L_archive_est_effacee_apres_7_jours()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var host = factory.WithClock(clock);
        var client = await host.CreateNewUserClientAsync();
        await client.PostAsync("/api/me/export", null);
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);
        var export = (await client.GetFromJsonAsync<DataExportResponse>("/api/me/export", TestJson.Options))!;

        clock.Advance(DataExport.Lifetime);
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
        Assert.False(await db.DataExports.AnyAsync(e => e.Id == export.Id));
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => storage.OpenReadAsync($"export-{export.Id:N}.zip", CancellationToken.None));
    }
}