using System.Net;
using System.Net.Http.Json;
using System.Text;

using SongVault.Api.Contracts.Audit;
using SongVault.Api.Contracts.Songs;
using SongVault.Api.Contracts.Versions;
using SongVault.Domain.Audit;
using SongVault.Domain.Bands;

namespace SongVault.Api.Tests.Songs;

/// <summary>#22 : crédits, règle des 100 %, ISWC/ISRC et export CSV, contre SQL Server.</summary>
[Collection(ApiCollection.Name)]
public sealed class SongRightsEndpointsTests(SongVaultApiFactory factory)
{
    private sealed record SongRef(Guid SongId, Guid VersionId, string SongUrl);

    private static async Task<SongRef> SongAsync(HttpClient client, Guid bandId, string title)
    {
        var created = await client.PostAsJsonAsync($"/api/bands/{bandId}/songs", new { title });
        var song = (await created.Content.ReadFromJsonAsync<SongResponse>(TestJson.Options))!;
        var songUrl = created.Headers.Location!.ToString();
        var version = await client.PostAsJsonAsync($"{songUrl}/versions", new { title = "Mix final", status = "Final" });
        var v = (await version.Content.ReadFromJsonAsync<SongVersionResponse>(TestJson.Options))!;
        return new SongRef(song.Id, v.Id, songUrl);
    }

    private static object Credit(string name, string role, decimal sharePercent) => new { name, role, sharePercent };

    private static Task<HttpResponseMessage> SetRightsAsync(HttpClient client, SongRef song, string? iswc, params object[] credits)
        => client.PutAsJsonAsync($"{song.SongUrl}/rights", new { iswc, credits });

    private static async Task<SongRightsResponse> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SongRightsResponse>(TestJson.Options))!;
    }

    [Fact]
    public async Task Les_credits_a_100_pourcent_et_l_iswc_sont_enregistres_et_journalises()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var aube = await SongAsync(owner, bandId, "Aube");

        var saved = await ReadAsync(await SetRightsAsync(owner, aube, "T0345246801",
            Credit("Alice", "Author", 33.33m), Credit("Bruno", "Composer", 33.33m), Credit("Éditions du Nord", "Publisher", 33.34m)));

        Assert.Equal("T-034.524.680-1", saved.Iswc);
        Assert.Equal(100m, saved.TotalPercent);
        var reloaded = (await owner.GetFromJsonAsync<SongRightsResponse>($"{aube.SongUrl}/rights", TestJson.Options))!;
        Assert.Equal(["Alice", "Bruno", "Éditions du Nord"], reloaded.Credits.Select(c => c.Name));
        Assert.Equal([33.33m, 33.33m, 33.34m], reloaded.Credits.Select(c => c.SharePercent));
        var audit = (await owner.GetFromJsonAsync<AuditPageResponse>($"/api/bands/{bandId}/audit", TestJson.Options))!;
        var entry = audit.Items.First(i => i.Action == AuditAction.CreditsChanged);
        Assert.Equal(("Aube", "3 crédits, ISWC T-034.524.680-1"), (entry.Subject, entry.Details));
    }

    [Fact]
    public async Task Un_total_a_99_pourcent_est_refuse_et_rien_n_est_enregistre()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var aube = await SongAsync(owner, bandId, "Aube");
        await ReadAsync(await SetRightsAsync(owner, aube, null, Credit("Alice", "Author", 100m)));

        var refused = await SetRightsAsync(owner, aube, null, Credit("Alice", "Author", 50m), Credit("Bruno", "Composer", 49m));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains("99 %", await refused.Content.ReadAsStringAsync());
        var kept = (await owner.GetFromJsonAsync<SongRightsResponse>($"{aube.SongUrl}/rights", TestJson.Options))!;
        Assert.Equal(("Alice", 100m), (kept.Credits.Single().Name, kept.TotalPercent));
    }

    [Fact]
    public async Task Les_saisies_invalides_sont_refusees()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var aube = await SongAsync(owner, bandId, "Aube");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await SetRightsAsync(owner, aube, null, new { name = "Alice", sharePercent = 100m })).StatusCode);   // rôle manquant
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SetRightsAsync(owner, aube, null, Credit("Alice", "Chanteur", 100m))).StatusCode);              // rôle inconnu
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SetRightsAsync(owner, aube, "T-034.524.680-2", Credit("Alice", "Author", 100m))).StatusCode);  // clé ISWC
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SetRightsAsync(owner, aube, null, Credit("Alice", "Author", 33.333m), Credit("Bruno", "Composer", 66.667m))).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<SongRightsResponse>($"{aube.SongUrl}/rights", TestJson.Options))!.Credits);
    }

    [Fact]
    public async Task L_isrc_se_pose_sur_la_version_et_se_retire()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var aube = await SongAsync(owner, bandId, "Aube");
        var isrcUrl = $"{aube.SongUrl}/versions/{aube.VersionId}/isrc";

        var set = await owner.PutAsJsonAsync(isrcUrl, new { isrc = "frz032600001" });
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.Equal("FR-Z03-26-00001", (await set.Content.ReadFromJsonAsync<VersionIsrcResponse>(TestJson.Options))!.Isrc);
        var rights = (await owner.GetFromJsonAsync<SongRightsResponse>($"{aube.SongUrl}/rights", TestJson.Options))!;
        Assert.Equal("FR-Z03-26-00001", rights.Versions.Single().Isrc);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PutAsJsonAsync(isrcUrl, new { isrc = "FR-Z03-26-0001" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync(isrcUrl, new { isrc = "" })).StatusCode);
        Assert.Null((await owner.GetFromJsonAsync<SongRightsResponse>($"{aube.SongUrl}/rights", TestJson.Options))!.Versions.Single().Isrc);
    }

    [Fact]
    public async Task L_export_csv_du_morceau_est_lisible_par_Excel()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var aube = await SongAsync(owner, bandId, "Aube");
        await ReadAsync(await SetRightsAsync(owner, aube, "T-034.524.680-1",
            Credit("Alice", "Author", 33.33m), Credit("Bruno", "Composer", 33.33m), Credit("Chloé", "Arranger", 33.34m)));

        var response = await owner.GetAsync($"{aube.SongUrl}/rights.csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("credits-Aube.csv", response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n");
        Assert.Equal("Morceau;ISWC;Ayant droit;Rôle;Part (%)", lines[0]);
        Assert.Equal("Aube;T-034.524.680-1;Chloé;Arrangeur;33,34", lines[3]);
    }

    [Fact]
    public async Task L_export_csv_de_la_sortie_reprend_chaque_piste_avec_son_isrc()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var aube = await SongAsync(owner, bandId, "Aube");
        var brume = await SongAsync(owner, bandId, "Brume");
        await ReadAsync(await SetRightsAsync(owner, aube, null, Credit("Alice", "Author", 60m), Credit("Bruno", "Composer", 40m)));
        await owner.PutAsJsonAsync($"{aube.SongUrl}/versions/{aube.VersionId}/isrc", new { isrc = "FR-Z03-26-00001" });
        var created = await owner.PostAsJsonAsync($"/api/bands/{bandId}/releases", new { title = "Nuits blanches", type = "EP" });
        var release = created.Headers.Location!.ToString();
        var tracks = await owner.PutAsJsonAsync($"{release}/tracks", new
        {
            tracks = new[] { new { songId = aube.SongId, versionId = aube.VersionId }, new { songId = brume.SongId, versionId = brume.VersionId } },
        });
        Assert.Equal(HttpStatusCode.OK, tracks.StatusCode);

        var csv = Encoding.UTF8.GetString(await owner.GetByteArrayAsync($"{release}/credits.csv")).TrimStart('\uFEFF');

        Assert.Equal([
            "Piste;Morceau;Version;ISRC;ISWC;Ayant droit;Rôle;Part (%)",
            "1;Aube;v1 Mix final;FR-Z03-26-00001;;Alice;Auteur;60",
            "1;Aube;v1 Mix final;FR-Z03-26-00001;;Bruno;Compositeur;40",
            "2;Brume;v1 Mix final;;;;;",
            ""], csv.Split("\r\n"));
    }

    [Fact]
    public async Task Un_Guest_lit_et_exporte_les_droits_mais_ne_les_modifie_pas()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var aube = await SongAsync(owner, bandId, "Aube");
        await ReadAsync(await SetRightsAsync(owner, aube, null, Credit("Alice", "Author", 100m)));
        var guest = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, guest, BandRole.Guest);

        Assert.Single((await guest.GetFromJsonAsync<SongRightsResponse>($"{aube.SongUrl}/rights", TestJson.Options))!.Credits);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"{aube.SongUrl}/rights.csv")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SetRightsAsync(guest, aube, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await guest.PutAsJsonAsync($"{aube.SongUrl}/versions/{aube.VersionId}/isrc", new { isrc = "FR-Z03-26-00001" })).StatusCode);
    }

    [Fact]
    public async Task Les_droits_d_un_morceau_d_un_autre_groupe_sont_introuvables()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var aube = await SongAsync(owner, await owner.FirstBandIdAsync(), "Aube");
        var stranger = await factory.CreateNewUserClientAsync();
        var strangerBand = await stranger.FirstBandIdAsync();

        var foreignUrl = $"/api/bands/{strangerBand}/songs/{aube.SongId}";
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{foreignUrl}/rights")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{foreignUrl}/rights.csv")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.PutAsJsonAsync($"{foreignUrl}/rights", new { credits = Array.Empty<object>() })).StatusCode);
    }
}