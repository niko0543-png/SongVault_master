using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SongVault.Api.Contracts.Comments;
using SongVault.Api.Contracts.Files;
using SongVault.Domain.Bands;

namespace SongVault.Api.Tests.Comments;

/// <summary>Commentaires et droits du compte : export et purge (ADR 0011, ADR 0014).</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public sealed class CommentsAccountTests(SongVaultApiFactory factory)
{
    /// <summary>URL des commentaires d'un MP3 déposé par le client dans son groupe personnel.</summary>
    private static async Task<(Guid BandId, string Comments)> AudioFileAsync(HttpClient owner)
    {
        var bandId = await owner.FirstBandIdAsync();
        var song = (await owner.PostAsJsonAsync($"/api/bands/{bandId}/songs", new { title = "Nocturne" }))
            .Headers.Location!.ToString();
        var version = (await owner.PostAsJsonAsync($"{song}/versions", new { title = "v1", status = "Demo" }))
            .Headers.Location!.ToString();
        var upload = await owner.PostAsync($"{version}/files", TestFiles.Form(TestFiles.Mp3(1024), "maquette.mp3"));
        var file = (await upload.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!;
        return (bandId, $"{version}/files/{file.Id}/comments");
    }

    [Fact]
    public async Task L_export_contient_les_commentaires_ecrits_par_le_compte()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var (bandId, comments) = await AudioFileAsync(owner);
        var guest = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, guest, BandRole.Guest);
        await guest.PostAsJsonAsync(comments, new { body = "À 1:42, la basse décale", positionMs = 102_000 });

        await guest.PostAsync("/api/me/export", null);
        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);

        var download = await guest.GetAsync("/api/me/export/download");
        using var zip = new ZipArchive(new MemoryStream(await download.Content.ReadAsByteArrayAsync()));
        var json = await new StreamReader(zip.GetEntry("data.json")!.Open()).ReadToEndAsync();
        Assert.Contains("À 1:42, la basse décale", json);
        Assert.Contains("\"positionMs\": 102000", json);
        Assert.Contains("\"songTitle\": \"Nocturne\"", json);
    }

    [Fact]
    public async Task La_purge_garde_les_commentaires_des_groupes_partages_sans_auteur()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var host = factory.WithClock(clock);
        var ownerEmail = $"owner-{Guid.NewGuid():N}@test.local";
        var owner = await host.CreateNewUserClientAsync(ownerEmail);
        var (bandId, comments) = await AudioFileAsync(owner);
        var member = await host.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, member, BandRole.Member);
        var created = await member.PostAsJsonAsync(comments, new { body = "Trop de reverb", positionMs = 30_000 });
        var comment = (await created.Content.ReadFromJsonAsync<CommentResponse>(TestJson.Options))!;
        await member.PostAsync($"{comments}/{comment.Id}/resolve", null);

        var deleted = await member.PostAsJsonAsync("/api/me/delete", new { password = SongVaultApiFactory.TestPassword });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        clock.Advance(TimeSpan.FromDays(30));
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);

        // Le cookie de l'Owner a expiré pendant ces 30 jours (durée de vie 7 jours) : il se reconnecte
        owner = await LoginAsync(host, ownerEmail);
        var thread = Assert.Single((await owner.GetFromJsonAsync<List<CommentResponse>>(comments, TestJson.Options))!);
        Assert.Equal("Trop de reverb", thread.Body);
        Assert.Null(thread.AuthorId);
        Assert.Null(thread.AuthorEmail);
        Assert.NotNull(thread.ResolvedAt);            // le fil reste résolu…
        Assert.Null(thread.ResolvedByEmail);          // … sans dire par qui
    }

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> host, string email)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=true",
            new { email, password = SongVaultApiFactory.TestPassword });
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Add("Cookie", login.Headers.GetValues("Set-Cookie").First().Split(';', 2)[0]);
        return client;
    }
}