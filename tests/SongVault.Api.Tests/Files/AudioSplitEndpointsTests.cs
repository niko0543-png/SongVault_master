using System.Net;
using System.Net.Http.Json;

using SongVault.Api.Contracts.Files;
using SongVault.Api.Contracts.Songs;
using SongVault.Api.Contracts.Versions;
using SongVault.Domain.Files;

namespace SongVault.Api.Tests.Files;

/// <summary>Découpage d'un enregistrement en morceaux (ADR 0050), avec le vrai ffmpeg comme pour l'aperçu (#15).</summary>
[Collection(ApiCollection.Name)]
public sealed class AudioSplitEndpointsTests(SongVaultApiFactory factory)
{
    private static async Task<(string VersionUrl, SongFileResponse File)> UploadAsync(HttpClient client, double seconds)
    {
        var versionUrl = await client.CreateVersionUrlAsync("Répétition du 24/10/2026");
        var response = await client.PostAsync($"{versionUrl}/files", TestFiles.Form(TestFiles.Wav(seconds), "repet.wav"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (versionUrl, (await response.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!);
    }

    [Fact]
    public async Task Chaque_passage_devient_une_version_avec_son_propre_fichier_audio()
    {
        var client = await factory.CreateNewUserClientAsync();
        var (versionUrl, file) = await UploadAsync(client, 10);
        var splitsUrl = $"{versionUrl}/files/{file.Id}/splits";
        var songs = await client.SongsUrlAsync();
        var target = (await (await client.PostAsJsonAsync(songs, new { title = "Nocturne" }))
            .Content.ReadFromJsonAsync<SongResponse>(TestJson.Options))!;
        var body = new
        {
            parts = new object[]
            {
                new { startMs = 0, endMs = 3000, title = "Intro" },
                new { startMs = 4000, endMs = 9000, title = "Nocturne", songId = target.Id },
            },
        };
        // L'aperçu n'est pas prêt : la durée du fichier n'est pas connue
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(splitsUrl, body)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync($"{splitsUrl}/latest")).StatusCode);
        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);       // antivirus, puis aperçu

        var tooLong = new { parts = new[] { new { startMs = 0, endMs = 11_000, title = "Trop long" } } };
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(splitsUrl, tooLong)).StatusCode);
        var accepted = await client.PostAsJsonAsync(splitsUrl, body);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.Equal(AudioSplitStatus.Pending,
            (await accepted.Content.ReadFromJsonAsync<AudioSplitResponse>(TestJson.Options))!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(splitsUrl, body)).StatusCode);

        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);       // découpage, puis antivirus et aperçu des passages

        var split = (await client.GetFromJsonAsync<AudioSplitResponse>($"{splitsUrl}/latest", TestJson.Options))!;
        Assert.Equal(AudioSplitStatus.Done, split.Status);
        Assert.Equal(target.Id, split.Parts[1].CreatedSongId);
        Assert.NotEqual(target.Id, split.Parts[0].CreatedSongId);
        var intro = await client.GetFromJsonAsync<SongResponse>($"{songs}/{split.Parts[0].CreatedSongId}", TestJson.Options);
        Assert.Equal("Intro", intro!.Title);

        foreach (var (part, expectedMs) in split.Parts.Zip([3000, 5000]))
        {
            var url = $"{songs}/{part.CreatedSongId}/versions/{part.CreatedVersionId}";
            var version = (await client.GetFromJsonAsync<SongVersionResponse>(url, TestJson.Options))!;
            Assert.Equal("Extrait de repet", version.Title);
            var piece = Assert.Single(version.Files);
            Assert.Equal($"{part.Title} (repet).wav", piece.OriginalFileName);
            Assert.Equal(AudioPreviewStatus.Ready, piece.Preview);
            var waveform = await client.GetFromJsonAsync<WaveformResponse>($"{url}/files/{piece.Id}/waveform", TestJson.Options);
            // WAV recopié sans réencodage : coupé au paquet près (≈ 0,1 s pour ce petit WAV mono à 22 kHz)
            Assert.InRange(waveform!.DurationMs, expectedMs - 150, expectedMs + 150);
        }
        // L'original reste en place, tel quel
        var source = (await client.GetFromJsonAsync<SongVersionResponse>(versionUrl, TestJson.Options))!;
        Assert.Equal(file.SizeBytes, Assert.Single(source.Files).SizeBytes);
    }

    [Fact]
    public async Task Un_invite_ne_decoupe_pas()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var (versionUrl, file) = await UploadAsync(owner, 2);
        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);
        var guest = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(await owner.FirstBandIdAsync(), guest, Domain.Bands.BandRole.Guest);

        var response = await guest.PostAsJsonAsync($"{versionUrl}/files/{file.Id}/splits",
            new { parts = new[] { new { startMs = 0, endMs = 1500, title = "A" } } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
