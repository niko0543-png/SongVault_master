using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;

using Microsoft.Extensions.DependencyInjection;

using SongVault.Api.Contracts.Files;
using SongVault.Api.Contracts.Versions;
using SongVault.Application.Abstractions;
using SongVault.Domain.Files;

namespace SongVault.Api.Tests.Files;

/// <summary>
/// #15 : aperçu MP3 et forme d'onde, préparés par la tâche "audio.preview" avec le vrai ffmpeg
/// (il doit être installé sur la machine qui lance les tests, et dans la CI).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AudioPreviewTests(SongVaultApiFactory factory)
{
    private static async Task<(string VersionUrl, SongFileResponse File)> UploadAsync(
        HttpClient client, byte[] bytes, string fileName)
    {
        var versionUrl = await client.CreateVersionUrlAsync("Aperçu");
        var response = await client.PostAsync($"{versionUrl}/files", TestFiles.Form(bytes, fileName));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (versionUrl, (await response.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!);
    }

    private static async Task<AudioPreviewStatus?> PreviewStatusAsync(HttpClient client, string versionUrl, Guid fileId)
    {
        var version = await client.GetFromJsonAsync<SongVersionResponse>(versionUrl, TestJson.Options);
        return version!.Files.Single(f => f.Id == fileId).Preview;
    }

    [Fact]
    public async Task Un_WAV_recoit_un_apercu_MP3_et_une_forme_d_onde_et_garde_son_original()
    {
        var client = await factory.CreateNewUserClientAsync();
        var wav = TestFiles.Wav(2);
        var (versionUrl, file) = await UploadAsync(client, wav, "prise.wav");
        Assert.Equal(AudioPreviewStatus.Pending, file.Preview);
        var fileUrl = $"{versionUrl}/files/{file.Id}";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{fileUrl}/waveform")).StatusCode);

        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);

        Assert.Equal(AudioPreviewStatus.Ready, await PreviewStatusAsync(client, versionUrl, file.Id));
        var listen = await client.GetAsync($"{fileUrl}/listen");
        Assert.Equal(HttpStatusCode.OK, listen.StatusCode);
        Assert.Equal("audio/mpeg", listen.Content.Headers.ContentType?.MediaType);
        Assert.True((await listen.Content.ReadAsByteArrayAsync()).Length < wav.Length);
        var waveform = await client.GetFromJsonAsync<WaveformResponse>($"{fileUrl}/waveform", TestJson.Options);
        Assert.Equal(AudioPreview.PeakCount, waveform!.Peaks.Count);
        Assert.InRange(waveform.DurationMs, 1990, 2010);
        Assert.Equal(255, waveform.Peaks.Max());
        // Le téléchargement reste l'original, octet pour octet
        var original = await client.GetByteArrayAsync($"{fileUrl}/content");
        Assert.Equal(SHA256.HashData(wav), SHA256.HashData(original));
    }

    [Fact]
    public async Task Un_MP3_garde_son_original_a_l_ecoute()
    {
        var client = await factory.CreateNewUserClientAsync();
        var (versionUrl, file) = await UploadAsync(client, TestFiles.Mp3(4096), "maquette.mp3");

        var listen = await client.GetAsync($"{versionUrl}/files/{file.Id}/listen");

        // Aperçu pas encore prêt (ou en échec : ces octets ne sont pas un vrai MP3) : l'original est servi
        Assert.Equal(HttpStatusCode.OK, listen.StatusCode);
        Assert.Equal(4096, (await listen.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task Un_fichier_illisible_passe_en_echec_apres_trois_essais_et_peut_etre_relance()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var host = factory.WithClock(clock);
        var client = await host.CreateNewUserClientAsync();
        var (versionUrl, file) = await UploadAsync(client, TestFiles.BrokenFlac(8192), "abime.flac");
        var previewUrl = $"{versionUrl}/files/{file.Id}/preview";
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(previewUrl, null)).StatusCode);

        await SongVaultApiFactory.RunDueJobsAsync(host.Services);
        Assert.Equal(AudioPreviewStatus.Pending, await PreviewStatusAsync(client, versionUrl, file.Id));  // 1 min avant le 2e essai
        clock.Advance(TimeSpan.FromMinutes(1));
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);
        clock.Advance(TimeSpan.FromMinutes(5));
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);

        Assert.Equal(AudioPreviewStatus.Failed, await PreviewStatusAsync(client, versionUrl, file.Id));
        // Écouter reste possible : l'original est servi
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{versionUrl}/files/{file.Id}/listen")).StatusCode);

        var retry = await client.PostAsync(previewUrl, null);
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        Assert.Equal(AudioPreviewStatus.Pending,
            (await retry.Content.ReadFromJsonAsync<AudioPreviewResponse>(TestJson.Options))!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(previewUrl, null)).StatusCode);
    }

    [Fact]
    public async Task Un_PDF_n_a_ni_apercu_ni_forme_d_onde()
    {
        var client = await factory.CreateNewUserClientAsync();
        var pdf = RandomNumberGenerator.GetBytes(1024);
        "%PDF-"u8.CopyTo(pdf);
        var (versionUrl, file) = await UploadAsync(client, pdf, "paroles.pdf");

        Assert.Null(file.Preview);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"{versionUrl}/files/{file.Id}/preview", null)).StatusCode);
    }

    [Fact]
    public async Task Supprimer_le_fichier_supprime_son_apercu()
    {
        var client = await factory.CreateNewUserClientAsync();
        var (versionUrl, file) = await UploadAsync(client, TestFiles.Wav(1), "court.wav");
        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);
        var storage = factory.Services.GetRequiredService<IFileStorageService>();
        var previewKey = AudioPreview.StorageKeyFor(file.Id);
        Assert.NotNull(await storage.GetLengthAsync(previewKey, CancellationToken.None));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{versionUrl}/files/{file.Id}")).StatusCode);

        Assert.Null(await storage.GetLengthAsync(previewKey, CancellationToken.None));
    }
}