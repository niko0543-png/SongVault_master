using System.Net;
using System.Net.Http.Json;

using SongVault.Api.Contracts.Files;
using SongVault.Domain.Bands;

namespace SongVault.Api.Tests.Files;

/// <summary>#29 : calage d'une tablature sur une maquette de la même version, contre SQL Server.</summary>
[Collection(ApiCollection.Name)]
public sealed class TabSyncEndpointsTests(SongVaultApiFactory factory)
{
    // En-tête d'un fichier Guitar Pro 5 (#28) : le contrôle de signature ne lit que le début
    private static readonly byte[] Gp5 = [.. "\u0018FICHIER GUITAR PRO v5.00"u8, .. new byte[64]];

    private static readonly object[] ThreeBars =
    [
        new { barIndex = 0, barOccurence = 0, barPosition = 0.0, millisecondOffset = 1000 },
        new { barIndex = 1, barOccurence = 0, barPosition = 0.0, millisecondOffset = 2500 },
        new { barIndex = 2, barOccurence = 0, barPosition = 0.0, millisecondOffset = 4100 },
    ];

    private static async Task<SongFileResponse> UploadAsync(HttpClient client, string versionUrl, byte[] bytes, string fileName)
    {
        var response = await client.PostAsync($"{versionUrl}/files", TestFiles.Form(bytes, fileName));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!;
    }

    private static async Task<(string VersionUrl, SongFileResponse Tab, SongFileResponse Demo)> VersionAsync(HttpClient owner)
    {
        var versionUrl = await owner.CreateVersionUrlAsync("Nocturne");
        var tab = await UploadAsync(owner, versionUrl, Gp5, "riff.gp5");
        var demo = await UploadAsync(owner, versionUrl, TestFiles.Wav(1), "maquette.wav");
        return (versionUrl, tab, demo);
    }

    private static Task<HttpResponseMessage> SetAsync(HttpClient client, string versionUrl, Guid fileId, Guid audioFileId, params object[] points)
        => client.PutAsJsonAsync($"{versionUrl}/files/{fileId}/sync", new { audioFileId, points });

    [Fact]
    public async Task Le_calage_est_enregistre_puis_relu_a_l_identique()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var (versionUrl, tab, demo) = await VersionAsync(owner);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.GetAsync($"{versionUrl}/files/{tab.Id}/sync")).StatusCode);

        var saved = await SetAsync(owner, versionUrl, tab.Id, demo.Id, ThreeBars);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var sync = (await owner.GetFromJsonAsync<TabSyncResponse>($"{versionUrl}/files/{tab.Id}/sync", TestJson.Options))!;
        Assert.Equal((demo.Id, "maquette.wav"), (sync.AudioFileId, sync.AudioFileName));
        Assert.Equal([1000, 2500, 4100], sync.Points.Select(p => p.MillisecondOffset));
        Assert.Equal([0, 1, 2], sync.Points.Select(p => p.BarIndex));
    }

    [Fact]
    public async Task Des_points_qui_reculent_sont_refuses_et_rien_ne_change()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var (versionUrl, tab, demo) = await VersionAsync(owner);
        await SetAsync(owner, versionUrl, tab.Id, demo.Id, ThreeBars);

        var refused = await SetAsync(owner, versionUrl, tab.Id, demo.Id,
            new { barIndex = 0, barOccurence = 0, barPosition = 0.0, millisecondOffset = 3000 },
            new { barIndex = 1, barOccurence = 0, barPosition = 0.0, millisecondOffset = 2000 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var sync = (await owner.GetFromJsonAsync<TabSyncResponse>($"{versionUrl}/files/{tab.Id}/sync", TestJson.Options))!;
        Assert.Equal(3, sync.Points.Count);
    }

    [Fact]
    public async Task Une_requete_incomplete_ou_hors_limites_rend_400()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var (versionUrl, tab, demo) = await VersionAsync(owner);

        var noPoints = await SetAsync(owner, versionUrl, tab.Id, demo.Id);
        var outOfRange = await SetAsync(owner, versionUrl, tab.Id, demo.Id,
            new { barIndex = 0, barOccurence = 0, barPosition = 2.0, millisecondOffset = 0 });
        var noAudio = await owner.PutAsJsonAsync($"{versionUrl}/files/{tab.Id}/sync", new { points = ThreeBars });

        Assert.Equal(HttpStatusCode.BadRequest, noPoints.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noAudio.StatusCode);
    }

    [Fact]
    public async Task La_maquette_doit_etre_audio_et_le_fichier_cale_une_tablature()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var (versionUrl, tab, demo) = await VersionAsync(owner);

        var notAudio = await SetAsync(owner, versionUrl, tab.Id, tab.Id, ThreeBars);
        var notTab = await SetAsync(owner, versionUrl, demo.Id, demo.Id, ThreeBars);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, notAudio.StatusCode);
        Assert.Contains("riff.gp5", await notAudio.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, notTab.StatusCode);
    }

    [Fact]
    public async Task Une_maquette_mise_a_la_corbeille_suspend_le_calage_qui_revient_a_sa_restauration()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var (versionUrl, tab, demo) = await VersionAsync(owner);
        await SetAsync(owner, versionUrl, tab.Id, demo.Id, ThreeBars);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"{versionUrl}/files/{demo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.GetAsync($"{versionUrl}/files/{tab.Id}/sync")).StatusCode);

        var restored = await owner.PostAsync($"/api/bands/{bandId}/trash/file/{demo.Id}/restore", null);
        Assert.True(restored.IsSuccessStatusCode, restored.StatusCode.ToString());
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"{versionUrl}/files/{tab.Id}/sync")).StatusCode);
    }

    [Fact]
    public async Task Supprimer_le_calage_rend_la_tablature_a_son_propre_son()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var (versionUrl, tab, demo) = await VersionAsync(owner);
        await SetAsync(owner, versionUrl, tab.Id, demo.Id, ThreeBars);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"{versionUrl}/files/{tab.Id}/sync")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.GetAsync($"{versionUrl}/files/{tab.Id}/sync")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"{versionUrl}/files/{tab.Id}/sync")).StatusCode);
    }

    [Fact]
    public async Task Un_Guest_lit_le_calage_mais_ne_le_modifie_pas()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var (versionUrl, tab, demo) = await VersionAsync(owner);
        await SetAsync(owner, versionUrl, tab.Id, demo.Id, ThreeBars);
        var guest = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, guest, BandRole.Guest);

        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"{versionUrl}/files/{tab.Id}/sync")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SetAsync(guest, versionUrl, tab.Id, demo.Id, ThreeBars)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.DeleteAsync($"{versionUrl}/files/{tab.Id}/sync")).StatusCode);
    }

    [Fact]
    public async Task Le_calage_d_un_autre_groupe_est_introuvable()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var (versionUrl, tab, demo) = await VersionAsync(owner);
        var stranger = await factory.CreateNewUserClientAsync();
        var foreign = versionUrl.Replace(bandId.ToString(), (await stranger.FirstBandIdAsync()).ToString());

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{foreign}/files/{tab.Id}/sync")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SetAsync(stranger, foreign, tab.Id, demo.Id, ThreeBars)).StatusCode);
    }
}