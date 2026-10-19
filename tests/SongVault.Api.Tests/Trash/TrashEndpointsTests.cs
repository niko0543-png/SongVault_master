using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Api.Contracts.Files;
using SongVault.Api.Contracts.Songs;
using SongVault.Api.Contracts.Trash;
using SongVault.Api.Contracts.Versions;
using SongVault.Api.Tests.Billing;
using SongVault.Application.Abstractions;
using SongVault.Application.Trash;
using SongVault.Domain.Audit;
using SongVault.Domain.Bands;
using SongVault.Domain.Trash;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests.Trash;

[Collection(ApiCollection.Name)]
public sealed class TrashEndpointsTests(SongVaultApiFactory factory)
{
    private sealed record Created(Guid BandId, string SongUrl, Guid SongId, string VersionUrl, Guid VersionId, Guid FileId, string StorageKey);

    /// <summary>Un morceau, une version et un MP3 de 4096 octets, analysé par l'antivirus (#17), dans le premier groupe du client.</summary>
    private async Task<Created> SongWithFileAsync(HttpClient client, string title = "Nocturne")
    {
        var bandId = await client.FirstBandIdAsync();
        var song = await client.PostAsJsonAsync($"/api/bands/{bandId}/songs", new { title });
        var songId = (await song.Content.ReadFromJsonAsync<SongResponse>(TestJson.Options))!.Id;
        var version = await client.PostAsJsonAsync($"{song.Headers.Location}/versions", new { title = "Maquette", status = "Demo" });
        var versionId = (await version.Content.ReadFromJsonAsync<SongVersionResponse>(TestJson.Options))!.Id;
        var upload = await client.PostAsync($"{version.Headers.Location}/files", TestFiles.Form(TestFiles.Mp3(4096), "prise.mp3"));
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var fileId = (await upload.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!.Id;
        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);
        using var scope = factory.Services.CreateScope();
        var key = await scope.ServiceProvider.GetRequiredService<SongVaultDbContext>().Set<Domain.Files.SongFile>()
            .IgnoreQueryFilters().Where(f => f.Id == fileId).Select(f => f.StorageKey).SingleAsync();
        return new Created(bandId, song.Headers.Location!.ToString(), songId, version.Headers.Location!.ToString(), versionId, fileId, key);
    }

    private static async Task<List<TrashItemResponse>> TrashAsync(HttpClient client, Guid bandId)
        => (await client.GetFromJsonAsync<List<TrashItemResponse>>($"/api/bands/{bandId}/trash", TestJson.Options))!;

    private async Task<long?> StoredLengthAsync(string key)
        => await factory.Services.GetRequiredService<IFileStorageService>().GetLengthAsync(key, CancellationToken.None);

    [Fact]
    public async Task Un_morceau_supprime_va_a_la_corbeille_et_revient_en_entier()
    {
        var client = await factory.CreateNewUserClientAsync();
        var c = await SongWithFileAsync(client);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(c.SongUrl)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(c.SongUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{c.VersionUrl}/files/{c.FileId}/content")).StatusCode);
        var item = Assert.Single(await TrashAsync(client, c.BandId));
        Assert.Equal((TrashItemKind.Song, c.SongId, "Nocturne", 4096L), (item.Kind, item.Id, item.SongTitle, item.SizeBytes));
        Assert.Equal(item.DeletedAt.AddDays(30), item.PurgeAt);
        Assert.NotNull(item.DeletedByEmail);
        Assert.Equal(4096, await factory.StorageUsedAsync(c.BandId));     // la corbeille compte jusqu'à la purge
        Assert.Equal(4096, await StoredLengthAsync(c.StorageKey));

        var restored = await client.PostAsync($"/api/bands/{c.BandId}/trash/song/{c.SongId}/restore", null);

        Assert.Equal(HttpStatusCode.NoContent, restored.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(c.SongUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(c.VersionUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{c.VersionUrl}/files/{c.FileId}/content")).StatusCode);
        Assert.Empty(await TrashAsync(client, c.BandId));
    }

    [Fact]
    public async Task Une_version_supprimee_disparait_de_son_morceau_puis_revient()
    {
        var client = await factory.CreateNewUserClientAsync();
        var c = await SongWithFileAsync(client);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(c.VersionUrl)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(c.VersionUrl)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<SongVersionSummaryResponse>>($"{c.SongUrl}/versions", TestJson.Options))!);
        var item = Assert.Single(await TrashAsync(client, c.BandId));
        Assert.Equal((TrashItemKind.Version, 1), (item.Kind, item.VersionNumber));

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsync($"/api/bands/{c.BandId}/trash/version/{c.VersionId}/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{c.VersionUrl}/files/{c.FileId}/content")).StatusCode);
    }

    [Fact]
    public async Task Une_version_d_un_morceau_supprime_ne_revient_qu_avec_lui_et_ce_qui_etait_parti_avant_reste()
    {
        var client = await factory.CreateNewUserClientAsync();
        var c = await SongWithFileAsync(client);
        await client.DeleteAsync(c.VersionUrl);
        await client.DeleteAsync(c.SongUrl);

        Assert.Equal(TrashItemKind.Song, Assert.Single(await TrashAsync(client, c.BandId)).Kind);   // la version est dedans
        var conflict = await client.PostAsync($"/api/bands/{c.BandId}/trash/version/{c.VersionId}/restore", null);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        await client.PostAsync($"/api/bands/{c.BandId}/trash/song/{c.SongId}/restore", null);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(c.SongUrl)).StatusCode);
        Assert.Equal(c.VersionId, Assert.Single(await TrashAsync(client, c.BandId)).Id);          // supprimée avant : elle reste
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task Un_Guest_ne_voit_pas_la_corbeille_et_un_Member_restaure_sans_supprimer_definitivement()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var c = await SongWithFileAsync(owner);
        var guest = await factory.CreateNewUserClientAsync();
        var member = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(c.BandId, guest, BandRole.Guest);
        await factory.AddMemberAsync(c.BandId, member, BandRole.Member);
        await owner.DeleteAsync($"{c.VersionUrl}/files/{c.FileId}");
        var trash = $"/api/bands/{c.BandId}/trash";

        Assert.Equal(HttpStatusCode.Forbidden, (await guest.GetAsync(trash)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.PostAsync($"{trash}/file/{c.FileId}/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.DeleteAsync($"{trash}/file/{c.FileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await member.PostAsync($"{trash}/file/{c.FileId}/restore", null)).StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task On_ne_touche_pas_a_la_corbeille_d_un_autre_groupe()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var c = await SongWithFileAsync(owner);
        await owner.DeleteAsync(c.SongUrl);
        var stranger = await factory.CreateNewUserClientAsync();
        var hisBand = await stranger.FirstBandIdAsync();

        Assert.Empty(await TrashAsync(stranger, hisBand));
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/bands/{hisBand}/trash/song/{c.SongId}/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/bands/{hisBand}/trash/song/{c.SongId}")).StatusCode);
        Assert.Equal(4096, await StoredLengthAsync(c.StorageKey));
    }

    [Fact]
    public async Task L_Owner_supprime_definitivement_et_la_place_se_libere()
    {
        var client = await factory.CreateNewUserClientAsync();
        var c = await SongWithFileAsync(client);
        await client.DeleteAsync(c.SongUrl);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/bands/{c.BandId}/trash/song/{c.SongId}")).StatusCode);

        Assert.Empty(await TrashAsync(client, c.BandId));
        Assert.Equal(0, await factory.StorageUsedAsync(c.BandId));
        Assert.Null(await StoredLengthAsync(c.StorageKey));
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsync($"/api/bands/{c.BandId}/trash/song/{c.SongId}/restore", null)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
        Assert.False(await db.Songs.IgnoreQueryFilters().AnyAsync(s => s.Id == c.SongId));
        Assert.False(await db.Set<Domain.Files.SongFile>().IgnoreQueryFilters().AnyAsync(f => f.Id == c.FileId));
    }

    [Fact]
    public async Task La_purge_efface_ce_qui_est_a_la_corbeille_depuis_trente_jours_et_rien_d_autre()
    {
        var client = await factory.CreateNewUserClientAsync();
        var old = await SongWithFileAsync(client, "Ancien");
        var recent = await SongWithFileAsync(client, "Récent");
        await client.DeleteAsync($"{old.VersionUrl}/files/{old.FileId}");
        await client.DeleteAsync($"{recent.VersionUrl}/files/{recent.FileId}");
        using (var scope = factory.Services.CreateScope())                 // « Ancien » : supprimé il y a 30 jours
        {
            var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
            await db.Set<Domain.Files.SongFile>().IgnoreQueryFilters().Where(f => f.Id == old.FileId)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.DeletedAt, f => f.DeletedAt!.Value.AddDays(-30)));
        }

        using (var scope = factory.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<PurgeExpiredTrashHandler>().HandleAsync(CancellationToken.None) >= 1);

        Assert.Null(await StoredLengthAsync(old.StorageKey));
        Assert.Equal(4096, await StoredLengthAsync(recent.StorageKey));
        Assert.Equal(4096, await factory.StorageUsedAsync(old.BandId));   // il reste « Récent »
        Assert.Equal(recent.FileId, Assert.Single(await TrashAsync(client, old.BandId)).Id);
        using var check = factory.Services.CreateScope();
        var purged = await check.ServiceProvider.GetRequiredService<SongVaultDbContext>().AuditEntries
            .IgnoreQueryFilters().SingleAsync(e => e.BandId == old.BandId && e.Action == AuditAction.FilePurged);
        Assert.Equal((null, "Ancien — v1 — prise.mp3"), (purged.ActorId, purged.Subject));
    }

    [Fact]
    public async Task Un_lien_de_partage_d_une_version_a_la_corbeille_ne_mene_plus_a_rien()
    {
        var client = await factory.CreateNewUserClientAsync();
        var c = await SongWithFileAsync(client);
        var created = await client.PostAsJsonAsync($"/api/bands/{c.BandId}/share-links",
            new { targetType = "Version", targetId = c.VersionId, label = "Studio" });
        var url = (await created.Content.ReadFromJsonAsync<SongVault.Api.Contracts.Sharing.ShareLinkCreatedResponse>(TestJson.Options))!.Url;
        var api = $"/api/share/{url[(url.LastIndexOf('/') + 1)..]}";
        var visitor = factory.CreateClient();

        await client.DeleteAsync(c.VersionUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(api)).StatusCode);

        await client.PostAsync($"/api/bands/{c.BandId}/trash/version/{c.VersionId}/restore", null);
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync(api)).StatusCode);
    }
}