using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Api.Contracts.Files;
using SongVault.Api.Contracts.Releases;
using SongVault.Api.Contracts.Songs;
using SongVault.Api.Contracts.Versions;
using SongVault.Application.Abstractions;
using SongVault.Domain.Bands;
using SongVault.Domain.Releases;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests.Releases;

/// <summary>#24 : archive complète d'une sortie, contre SQL Server et le stockage local.</summary>
[Collection(ApiCollection.Name)]
public sealed class ReleaseExportEndpointsTests(SongVaultApiFactory factory)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private sealed record Track(Guid SongId, Guid VersionId, string SongUrl, string VersionUrl);

    private static async Task<Track> SongAsync(HttpClient client, Guid bandId, string title, string? lyrics = null)
    {
        var created = await client.PostAsJsonAsync($"/api/bands/{bandId}/songs", new { title });
        var song = (await created.Content.ReadFromJsonAsync<SongResponse>(TestJson.Options))!;
        var songUrl = created.Headers.Location!.ToString();
        var version = await client.PostAsJsonAsync($"{songUrl}/versions", new { title = "Mix final", status = "Final", lyrics });
        var v = (await version.Content.ReadFromJsonAsync<SongVersionResponse>(TestJson.Options))!;
        return new Track(song.Id, v.Id, songUrl, version.Headers.Location!.ToString());
    }

    private static async Task<SongFileResponse> UploadAsync(HttpClient client, Track track, byte[] bytes, string fileName)
    {
        var response = await client.PostAsync($"{track.VersionUrl}/files", TestFiles.Form(bytes, fileName));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!;
    }

    private static async Task<string> ReleaseAsync(HttpClient client, Guid bandId, string title, params Track[] tracks)
    {
        var created = await client.PostAsJsonAsync($"/api/bands/{bandId}/releases", new { title, type = "EP" });
        var release = created.Headers.Location!.ToString();
        var set = await client.PutAsJsonAsync($"{release}/tracks",
            new { tracks = tracks.Select(t => new { songId = t.SongId, versionId = t.VersionId }) });
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        return release;
    }

    private static async Task<ReleaseExportResponse> RequestAsync(HttpClient client, string release)
    {
        var response = await client.PostAsync($"{release}/exports", null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReleaseExportResponse>(TestJson.Options))!;
    }

    private static async Task<string> EntryTextAsync(ZipArchive zip, string path)
    {
        using var reader = new StreamReader(zip.GetEntry(path)!.Open(), Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task Le_zip_contient_les_fichiers_de_la_version_choisie_les_paroles_les_credits_et_la_pochette()
    {
        var email = $"export-{Guid.NewGuid():N}@test.local";
        var owner = await factory.CreateNewUserClientAsync(email);
        var bandId = await owner.FirstBandIdAsync();
        var nuit = await SongAsync(owner, bandId, "Nuit / blanche", "Couplet 1\nRefrain");
        var master = TestFiles.Wav(1);
        await UploadAsync(owner, nuit, master, "master.wav");
        var voice = await UploadAsync(owner, nuit, TestFiles.Wav(1), "voix.wav");
        await UploadAsync(owner, nuit, "notes de studio"u8.ToArray(), "notes.txt");      // pas audio : pas dans l'archive
        await owner.PutAsJsonAsync($"{nuit.VersionUrl}/stems", new { stems = new[] { new { fileId = voice.Id, name = "Voix" } } });
        await owner.PutAsJsonAsync($"{nuit.SongUrl}/rights",
            new { iswc = (string?)null, credits = new[] { new { name = "Alice Martin", role = "Author", sharePercent = 100m } } });
        var draft = await owner.PostAsJsonAsync($"{nuit.SongUrl}/versions", new { title = "Brouillon", status = "Demo" });
        await owner.PostAsync($"{draft.Headers.Location}/files", TestFiles.Form(TestFiles.Wav(1), "brouillon.wav"));
        var aube = await SongAsync(owner, bandId, "Aube");
        var mp3 = TestFiles.Mp3(2048);
        await UploadAsync(owner, aube, mp3, "aube.mp3");
        var release = await ReleaseAsync(owner, bandId, "Nuits blanches", nuit, aube);
        await owner.PutAsync($"{release}/cover", TestFiles.Form(Png, "pochette.png"));
        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);           // analyses antivirus

        var requested = await RequestAsync(owner, release);
        Assert.Equal(ReleaseExportStatus.Pending, requested.Status);
        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);

        var ready = (await owner.GetFromJsonAsync<ReleaseExportResponse>($"{release}/exports/latest", TestJson.Options))!;
        Assert.Equal(ReleaseExportStatus.Ready, ready.Status);
        var download = await owner.GetAsync($"{release}/exports/{ready.Id}/archive");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/zip", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Nuits blanches.zip", download.Content.Headers.ContentDisposition?.FileNameStar);
        var bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.Equal(ready.SizeBytes, bytes.Length);

        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.Equal(
        [
            "Nuits blanches/01 - Nuit _ blanche/master.wav",
            "Nuits blanches/01 - Nuit _ blanche/Pistes séparées/1 - Voix.wav",
            "Nuits blanches/01 - Nuit _ blanche/paroles.txt",
            "Nuits blanches/02 - Aube/aube.mp3",
            "Nuits blanches/credits.csv",
            "Nuits blanches/pochette.png",
        ], zip.Entries.Select(e => e.FullName));
        using (var copy = new MemoryStream())
        {
            await zip.GetEntry("Nuits blanches/01 - Nuit _ blanche/master.wav")!.Open().CopyToAsync(copy);
            Assert.Equal(master, copy.ToArray());
        }
        Assert.Equal("Couplet 1\r\nRefrain", await EntryTextAsync(zip, "Nuits blanches/01 - Nuit _ blanche/paroles.txt"));
        Assert.Contains("1;Nuit / blanche;v1 Mix final;;;Alice Martin;Auteur;100", await EntryTextAsync(zip, "Nuits blanches/credits.csv"));

        var sent = factory.Emails.LastTo(email);
        Assert.Equal("Archive prête : Nuits blanches", sent.Subject);
        Assert.Contains($"/b/{bandId}/releases/{release.Split('/')[^1]}", sent.TextBody);   // la page, pas l'archive
    }

    [Fact]
    public async Task Un_lien_expire_est_refuse_avec_410_puis_l_archive_est_effacee()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var host = factory.WithClock(clock);
        var owner = await host.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var aube = await SongAsync(owner, bandId, "Aube");
        await UploadAsync(owner, aube, TestFiles.Mp3(1024), "aube.mp3");
        var release = await ReleaseAsync(owner, bandId, "Aube", aube);
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);
        var export = await RequestAsync(owner, release);
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"{release}/exports/{export.Id}/archive")).StatusCode);

        clock.Advance(ReleaseExport.Lifetime);
        var expired = await owner.GetAsync($"{release}/exports/{export.Id}/archive");

        Assert.Equal(HttpStatusCode.Gone, expired.StatusCode);
        Assert.Contains("Ce lien a expiré", await expired.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await owner.GetAsync($"{release}/exports/latest")).StatusCode);

        await SongVaultApiFactory.RunDueJobsAsync(host.Services);              // tâche d'expiration
        using var scope = host.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<SongVaultDbContext>().ReleaseExports.AnyAsync(e => e.Id == export.Id));
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IFileStorageService>()
            .GetLengthAsync(ReleaseExport.StorageKeyFor(export.Id), CancellationToken.None));
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"{release}/exports/{export.Id}/archive")).StatusCode);
    }

    [Fact]
    public async Task Une_sortie_incomplete_ou_une_archive_deja_en_preparation_sont_refusees()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var aube = await SongAsync(owner, bandId, "Aube");
        var empty = (await owner.PostAsJsonAsync($"/api/bands/{bandId}/releases", new { title = "Vide", type = "Single" })).Headers.Location!;
        var release = await ReleaseAsync(owner, bandId, "Aube", aube);

        var noTracks = await owner.PostAsync($"{empty}/exports", null);
        var noAudio = await owner.PostAsync($"{release}/exports", null);
        await UploadAsync(owner, aube, TestFiles.Mp3(1024), "aube.mp3");
        var scanning = await owner.PostAsync($"{release}/exports", null);         // analyse antivirus pas encore faite

        Assert.Equal(HttpStatusCode.Conflict, noTracks.StatusCode);
        Assert.Contains("aucune piste", await noTracks.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, noAudio.StatusCode);
        Assert.Contains("piste 1 « Aube »", await noAudio.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, scanning.StatusCode);
        Assert.Contains("« aube.mp3 » (piste 1)", await scanning.Content.ReadAsStringAsync());

        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);
        await RequestAsync(owner, release);
        var second = await owner.PostAsync($"{release}/exports", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("déjà en préparation", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_invite_voit_l_archive_sans_pouvoir_la_demander_et_un_autre_groupe_ne_la_trouve_pas()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var guest = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, guest, BandRole.Guest);
        var stranger = await factory.CreateNewUserClientAsync();
        var aube = await SongAsync(owner, bandId, "Aube");
        await UploadAsync(owner, aube, TestFiles.Mp3(1024), "aube.mp3");
        var release = await ReleaseAsync(owner, bandId, "Aube", aube);
        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);

        Assert.Equal(HttpStatusCode.Forbidden, (await guest.PostAsync($"{release}/exports", null)).StatusCode);
        var export = await RequestAsync(owner, release);
        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);

        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"{release}/exports/{export.Id}/archive")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{release}/exports/{export.Id}/archive")).StatusCode);
        var strangerBand = await stranger.FirstBandIdAsync();
        var elsewhere = release.Replace(bandId.ToString(), strangerBand.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{elsewhere}/exports/{export.Id}/archive")).StatusCode);
    }
}