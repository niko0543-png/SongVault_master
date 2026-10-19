using System.Net;
using System.Net.Http.Json;

using SongVault.Api.Contracts.Files;
using SongVault.Domain.Bands;

namespace SongVault.Api.Tests.Files;

/// <summary>#23 : pistes séparées d'une version, contre SQL Server (aperçus préparés par le vrai ffmpeg).</summary>
[Collection(ApiCollection.Name)]
public sealed class StemsEndpointsTests(SongVaultApiFactory factory)
{
    private static async Task<SongFileResponse> UploadAsync(HttpClient client, string versionUrl, byte[] bytes, string fileName)
    {
        var response = await client.PostAsync($"{versionUrl}/files", TestFiles.Form(bytes, fileName));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!;
    }

    private static object Stem(Guid fileId, string name) => new { fileId, name };

    private static Task<HttpResponseMessage> SetAsync(HttpClient client, string versionUrl, params object[] stems)
        => client.PutAsJsonAsync($"{versionUrl}/stems", new { stems });

    private static async Task<List<StemResponse>> ListAsync(HttpClient client, string versionUrl)
        => (await client.GetFromJsonAsync<List<StemResponse>>($"{versionUrl}/stems", TestJson.Options))!;

    [Fact]
    public async Task Les_pistes_sont_enregistrees_dans_l_ordre_et_jouables_une_fois_leur_apercu_pret()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var versionUrl = await owner.CreateVersionUrlAsync("Nocturne");
        var voice = await UploadAsync(owner, versionUrl, TestFiles.Wav(2), "voix.wav");
        var drums = await UploadAsync(owner, versionUrl, TestFiles.Wav(2), "batterie.wav");

        var saved = await SetAsync(owner, versionUrl, Stem(drums.Id, "Batterie"), Stem(voice.Id, " Voix "));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var pending = (await saved.Content.ReadFromJsonAsync<List<StemResponse>>(TestJson.Options))!;
        Assert.Equal([("Batterie", 0), ("Voix", 1)], pending.Select(s => (s.Name, s.Position)));
        Assert.All(pending, s => Assert.False(s.Playable));

        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);           // analyse antivirus puis aperçu MP3

        var ready = await ListAsync(owner, versionUrl);
        Assert.All(ready, s => Assert.True(s.Playable));
        Assert.All(ready, s => Assert.InRange(s.DurationMs!.Value, 1990, 2010));
        Assert.Equal("batterie.wav", ready[0].OriginalFileName);
    }

    [Fact]
    public async Task Un_fichier_qui_n_est_pas_audio_est_refuse_et_rien_ne_change()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var versionUrl = await owner.CreateVersionUrlAsync("Nocturne");
        var voice = await UploadAsync(owner, versionUrl, TestFiles.Wav(1), "voix.wav");
        var lyrics = await UploadAsync(owner, versionUrl, "Paroles du refrain"u8.ToArray(), "paroles.txt");
        Assert.Equal(HttpStatusCode.OK, (await SetAsync(owner, versionUrl, Stem(voice.Id, "Voix"))).StatusCode);

        var refused = await SetAsync(owner, versionUrl, Stem(lyrics.Id, "Paroles"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains("paroles.txt", await refused.Content.ReadAsStringAsync());
        Assert.Equal("Voix", (await ListAsync(owner, versionUrl)).Single().Name);
    }

    [Fact]
    public async Task Plus_de_8_pistes_ou_un_nom_manquant_sont_refuses()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var versionUrl = await owner.CreateVersionUrlAsync("Nocturne");

        var nine = await SetAsync(owner, versionUrl, [.. Enumerable.Range(1, 9).Select(i => Stem(Guid.NewGuid(), $"Piste {i}"))]);
        var noName = await owner.PutAsJsonAsync($"{versionUrl}/stems", new { stems = new[] { new { fileId = Guid.NewGuid() } } });

        Assert.Equal(HttpStatusCode.BadRequest, nine.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
    }

    [Fact]
    public async Task Un_fichier_mis_a_la_corbeille_quitte_le_melangeur()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var versionUrl = await owner.CreateVersionUrlAsync("Nocturne");
        var voice = await UploadAsync(owner, versionUrl, TestFiles.Wav(1), "voix.wav");
        var drums = await UploadAsync(owner, versionUrl, TestFiles.Wav(1), "batterie.wav");
        await SetAsync(owner, versionUrl, Stem(voice.Id, "Voix"), Stem(drums.Id, "Batterie"));

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"{versionUrl}/files/{voice.Id}")).StatusCode);

        var left = await ListAsync(owner, versionUrl);
        Assert.Equal([("Batterie", 0)], left.Select(s => (s.Name, s.Position)));
    }

    [Fact]
    public async Task Un_Guest_lit_les_pistes_mais_ne_les_modifie_pas()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var versionUrl = await owner.CreateVersionUrlAsync("Nocturne");
        var voice = await UploadAsync(owner, versionUrl, TestFiles.Wav(1), "voix.wav");
        await SetAsync(owner, versionUrl, Stem(voice.Id, "Voix"));
        var guest = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, guest, BandRole.Guest);

        Assert.Single(await ListAsync(guest, versionUrl));
        Assert.Equal(HttpStatusCode.Forbidden, (await SetAsync(guest, versionUrl)).StatusCode);
    }

    [Fact]
    public async Task Les_pistes_d_un_autre_groupe_sont_introuvables()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var versionUrl = await owner.CreateVersionUrlAsync("Nocturne");
        var stranger = await factory.CreateNewUserClientAsync();
        var foreign = versionUrl.Replace(bandId.ToString(), (await stranger.FirstBandIdAsync()).ToString());

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{foreign}/stems")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SetAsync(stranger, foreign)).StatusCode);
    }
}