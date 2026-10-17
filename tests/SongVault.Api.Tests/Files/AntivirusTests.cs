using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Api.Contracts.Files;
using SongVault.Api.Contracts.Versions;
using SongVault.Application.Abstractions;
using SongVault.Application.Files;
using SongVault.Domain.Bands;
using SongVault.Domain.Files;

namespace SongVault.Api.Tests.Files;

/// <summary>#17 : analyse antivirus en tâche de fond ("file.scan"), avec TestMalwareScanner à la place de ClamAV.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public sealed class AntivirusTests(SongVaultApiFactory factory)
{
    private static async Task<(string VersionUrl, SongFileResponse File)> UploadAsync(
        HttpClient client, byte[] bytes, string fileName)
    {
        var versionUrl = await client.CreateVersionUrlAsync("Antivirus");
        var response = await client.PostAsync($"{versionUrl}/files", TestFiles.Form(bytes, fileName));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (versionUrl, (await response.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!);
    }

    private static async Task<SongFileResponse> FileAsync(HttpClient client, string versionUrl, Guid fileId)
        => (await client.GetFromJsonAsync<SongVersionResponse>(versionUrl, TestJson.Options))!.Files.Single(f => f.Id == fileId);

    private static async Task<string> ConflictDetailAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail!;
    }

    [Fact]
    public async Task Un_fichier_reste_illisible_jusqu_a_son_analyse()
    {
        var client = await factory.CreateNewUserClientAsync();
        var bytes = TestFiles.Mp3(4096);
        var (versionUrl, file) = await UploadAsync(client, bytes, "maquette.mp3");
        var fileUrl = $"{versionUrl}/files/{file.Id}";

        Assert.Equal(FileScanStatus.Pending, file.Scan);
        Assert.Contains("Analyse antivirus en cours", await ConflictDetailAsync(await client.GetAsync($"{fileUrl}/content")));
        Assert.Contains("Analyse antivirus en cours", await ConflictDetailAsync(await client.GetAsync($"{fileUrl}/listen")));

        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);

        Assert.Equal(FileScanStatus.Clean, (await FileAsync(client, versionUrl, file.Id)).Scan);
        Assert.Equal(bytes, await client.GetByteArrayAsync($"{fileUrl}/content"));
    }

    [Fact]
    public async Task Un_fichier_infecte_est_bloque_et_son_auteur_prevenu()
    {
        var email = $"antivirus-{Guid.NewGuid():N}@test.local";
        var client = await factory.CreateNewUserClientAsync(email);
        var (versionUrl, file) = await UploadAsync(client, TestFiles.InfectedTxt(), "paroles.txt");
        var fileUrl = $"{versionUrl}/files/{file.Id}";

        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);

        var blocked = await FileAsync(client, versionUrl, file.Id);
        Assert.Equal(FileScanStatus.Infected, blocked.Scan);
        Assert.Equal(TestMalwareScanner.Threat, blocked.ScanThreat);
        Assert.Contains("Fichier bloqué", await ConflictDetailAsync(await client.GetAsync($"{fileUrl}/content?download=true")));
        Assert.Contains("supprimez-le", await ConflictDetailAsync(await client.PostAsync($"{fileUrl}/scan", null)));
        var notice = factory.Emails.LastTo(email);
        Assert.StartsWith("Fichier bloqué par l'antivirus", notice.Subject);
        Assert.Contains("paroles.txt", notice.TextBody);

        // Quarantaine : le fichier reste jusqu'à ce qu'un membre le supprime
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(fileUrl)).StatusCode);
        Assert.DoesNotContain((await client.GetFromJsonAsync<SongVersionResponse>(versionUrl, TestJson.Options))!.Files,
            f => f.Id == file.Id);
    }

    [Fact]
    public async Task Un_WAV_infecte_n_est_jamais_converti()
    {
        var client = await factory.CreateNewUserClientAsync();
        byte[] wav = [.. TestFiles.Wav(1), .. TestFiles.VirusMarker];
        var (versionUrl, file) = await UploadAsync(client, wav, "prise.wav");

        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);

        var blocked = await FileAsync(client, versionUrl, file.Id);
        Assert.Equal(FileScanStatus.Infected, blocked.Scan);
        Assert.Equal(AudioPreviewStatus.Failed, blocked.Preview);
        Assert.Null(await factory.Services.GetRequiredService<IFileStorageService>()
            .GetLengthAsync(AudioPreview.StorageKeyFor(file.Id), CancellationToken.None));
        Assert.Contains("antivirus", await ConflictDetailAsync(await client.PostAsync($"{versionUrl}/files/{file.Id}/preview", null)));
    }

    [Fact]
    public async Task Antivirus_injoignable_l_analyse_passe_en_echec_puis_se_relance()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var host = factory.WithClock(clock);
        var client = await host.CreateNewUserClientAsync();
        var (versionUrl, file) = await UploadAsync(client, TestFiles.Mp3(2048), "maquette.mp3");
        var scanUrl = $"{versionUrl}/files/{file.Id}/scan";

        factory.Scanner.Failure = new MalwareScannerException("ClamAV injoignable (test).");
        try
        {
            await SongVaultApiFactory.RunDueJobsAsync(host.Services);
            Assert.Equal(FileScanStatus.Pending, (await FileAsync(client, versionUrl, file.Id)).Scan);   // 1 min avant le 2e essai
            clock.Advance(TimeSpan.FromMinutes(1));
            await SongVaultApiFactory.RunDueJobsAsync(host.Services);
            clock.Advance(TimeSpan.FromMinutes(5));
            await SongVaultApiFactory.RunDueJobsAsync(host.Services);
        }
        finally
        {
            factory.Scanner.Failure = null;
        }

        Assert.Equal(FileScanStatus.Failed, (await FileAsync(client, versionUrl, file.Id)).Scan);
        Assert.Contains("relancez-la", await ConflictDetailAsync(await client.GetAsync($"{versionUrl}/files/{file.Id}/content")));

        var retry = await client.PostAsync(scanUrl, null);
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        Assert.Equal(FileScanStatus.Pending, (await retry.Content.ReadFromJsonAsync<FileScanResponse>(TestJson.Options))!.Status);
        Assert.Contains("déjà en cours", await ConflictDetailAsync(await client.PostAsync(scanUrl, null)));

        await SongVaultApiFactory.RunDueJobsAsync(host.Services);
        Assert.Equal(FileScanStatus.Clean, (await FileAsync(client, versionUrl, file.Id)).Scan);
    }

    [Fact]
    public async Task Un_invite_ne_peut_pas_relancer_une_analyse()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var (versionUrl, file) = await UploadAsync(owner, TestFiles.Mp3(1024), "a.mp3");
        var guest = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(await owner.FirstBandIdAsync(), guest, BandRole.Guest);

        var response = await guest.PostAsync($"{versionUrl}/files/{file.Id}/scan", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Antivirus_desactive_le_fichier_est_lisible_puis_analyse_a_l_activation()
    {
        var client = await factory.CreateNewUserClientAsync();
        var bytes = TestFiles.Mp3(4096);
        var (versionUrl, file) = await UploadAsync(client, bytes, "repetition.mp3");
        var fileUrl = $"{versionUrl}/files/{file.Id}";

        factory.Scanner.IsEnabled = false;                           // Antivirus:Provider None
        try
        {
            await SongVaultApiFactory.RunDueJobsAsync(factory.Services);
        }
        finally
        {
            factory.Scanner.IsEnabled = true;
        }

        Assert.Equal(FileScanStatus.NotScanned, (await FileAsync(client, versionUrl, file.Id)).Scan);
        Assert.Equal(bytes, await client.GetByteArrayAsync($"{fileUrl}/content"));
        Assert.Contains("pas encore activé", await ConflictDetailAsync(await client.PostAsync($"{fileUrl}/scan", null)));

        // Activation (Antivirus:Provider ClamAv) : ce que fait AntivirusActivationWorker au démarrage
        using (var scope = factory.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<RescanNotScannedFilesHandler>()
                .HandleAsync(CancellationToken.None) >= 1);
        Assert.Equal(FileScanStatus.Pending, (await FileAsync(client, versionUrl, file.Id)).Scan);

        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);

        Assert.Equal(FileScanStatus.Clean, (await FileAsync(client, versionUrl, file.Id)).Scan);
    }
}