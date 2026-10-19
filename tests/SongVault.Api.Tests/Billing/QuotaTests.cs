using System.Net;
using System.Net.Http.Json;
using SongVault.Api.Contracts.Files;
using Microsoft.AspNetCore.Mvc;
using SongVault.Api.Tests.Files;

namespace SongVault.Api.Tests.Billing;

[Collection(ApiCollection.Name)]
public sealed class QuotaTests(SongVaultApiFactory factory)
{
    [Fact]
    public async Task Un_envoi_au_dela_du_quota_repond_402_des_l_annonce()
    {
        var client = await factory.CreateNewUserClientAsync();
        var bandId = await client.FirstBandIdAsync();
        await factory.GrantTinyPlanAsync(bandId, quotaBytes: 100_000);
        var versionUrl = await client.CreateVersionUrlAsync();

        var direct = await client.StartAsync(versionUrl, "gros.mp3", 100_001);
        var multipart = await client.PostAsync($"{versionUrl}/files", TestFiles.Form(TestFiles.Mp3(100_001), "gros.mp3"));

        Assert.Equal(HttpStatusCode.PaymentRequired, direct.StatusCode);
        var problem = await direct.Content.ReadFromJsonAsync<ProblemDetails>(TestJson.Options);
        Assert.StartsWith("Stockage du groupe plein", problem!.Detail);
        Assert.Equal(HttpStatusCode.PaymentRequired, multipart.StatusCode);
    }

    [Fact]
    public async Task Deux_envois_annonces_en_meme_temps_le_second_est_refuse_a_la_confirmation()
    {
        var client = await factory.CreateNewUserClientAsync();
        var bandId = await client.FirstBandIdAsync();
        await factory.GrantTinyPlanAsync(bandId, quotaBytes: 100_000);
        var versionUrl = await client.CreateVersionUrlAsync();
        var bytes = TestFiles.Mp3(60_000);

        var first = await client.StartOkAsync(versionUrl, "a.mp3", bytes.Length);
        var second = await client.StartOkAsync(versionUrl, "b.mp3", bytes.Length);    // 0 utilisé : les deux passent
        await client.PutAsync(first, bytes);
        await client.PutAsync(second, bytes);

        Assert.Equal(HttpStatusCode.Created, (await client.CompleteAsync(versionUrl, first.UploadId)).StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, (await client.CompleteAsync(versionUrl, second.UploadId)).StatusCode);
        Assert.Equal(60_000, await factory.StorageUsedAsync(bandId));     // le second n'a pas compté
    }

    [Fact]
    public async Task Un_second_groupe_gratuit_repond_402_sauf_si_le_premier_est_abonne()
    {
        var client = await factory.CreateNewUserClientAsync();
        var personal = await client.FirstBandIdAsync();

        var refused = await client.PostAsJsonAsync("/api/bands", new { name = "Les Autres" });
        await factory.GrantPlanAsync(personal);
        var accepted = await client.PostAsJsonAsync("/api/bands", new { name = "Les Autres" });

        Assert.Equal(HttpStatusCode.PaymentRequired, refused.StatusCode);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task Le_stockage_suit_les_envois_et_les_suppressions()
    {
        var client = await factory.CreateNewUserClientAsync();
        var bandId = await client.FirstBandIdAsync();
        var versionUrl = await client.CreateVersionUrlAsync();

        var upload = await client.PostAsync($"{versionUrl}/files", TestFiles.Form(TestFiles.Mp3(4096), "a.mp3"));
        var file = (await upload.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!;
        var after = await factory.StorageUsedAsync(bandId);
        var deleted = await client.DeleteAsync($"{versionUrl}/files/{file.Id}");   // Location mène au contenu, pas au fichier
        var trashed = await factory.StorageUsedAsync(bandId);
        var purged = await client.DeleteAsync($"/api/bands/{bandId}/trash/file/{file.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, purged.StatusCode);

        Assert.Equal(4096, after);
        Assert.Equal(4096, trashed);                                     // #20 : comptée jusqu'à la suppression définitive
        Assert.Equal(0, await factory.StorageUsedAsync(bandId));
    }
}