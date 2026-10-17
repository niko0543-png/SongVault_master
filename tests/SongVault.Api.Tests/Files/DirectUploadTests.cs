using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Api.Contracts.Files;
using SongVault.Application.Abstractions;
using SongVault.Domain.Bands;
using SongVault.Domain.Files;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests.Files;

[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public sealed class DirectUploadTests(SongVaultApiFactory factory)
{
    [Fact]
    public async Task Annoncer_envoyer_confirmer_rattache_le_fichier_a_la_version()
    {
        var client = await factory.CreateNewUserClientAsync();
        var versionUrl = await client.CreateVersionUrlAsync();
        var bytes = TestFiles.Mp3(256 * 1024);

        var upload = await client.StartOkAsync(versionUrl, "../Prise 3.mp3", bytes.Length);
        Assert.StartsWith("/api/bands/", upload.UploadUrl);                 // stockage local : l'API reçoit le PUT
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync(upload, bytes)).StatusCode);
        var complete = await client.CompleteAsync(versionUrl, upload.UploadId);

        Assert.Equal(HttpStatusCode.Created, complete.StatusCode);
        var file = (await complete.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!;
        Assert.Equal("Prise 3.mp3", file.OriginalFileName);
        Assert.Equal(bytes.Length, file.SizeBytes);
        await SongVaultApiFactory.RunDueJobsAsync(factory.Services);          // #17 : analyse antivirus
        var downloaded = await client.GetByteArrayAsync(complete.Headers.Location);
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(downloaded));
        // Une seconde confirmation ne trouve plus l'envoi
        Assert.Equal(HttpStatusCode.NotFound, (await client.CompleteAsync(versionUrl, upload.UploadId)).StatusCode);
    }

    [Theory]
    [InlineData("virus.exe", 1024L, HttpStatusCode.BadRequest)]
    [InlineData("vide.mp3", 0L, HttpStatusCode.BadRequest)]
    [InlineData("long.wav", 2L * 1024 * 1024 * 1024 + 1, HttpStatusCode.RequestEntityTooLarge)]
    public async Task L_annonce_refuse_une_extension_un_fichier_vide_ou_trop_gros(string name, long size, HttpStatusCode expected)
    {
        var client = factory.CreateUserClient();
        var versionUrl = await client.CreateVersionUrlAsync();

        var response = await client.StartAsync(versionUrl, name, size);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task La_politique_annonce_2_Go()
    {
        var policy = await factory.CreateUserClient().GetFromJsonAsync<FilePolicyResponse>("/api/files/policy", TestJson.Options);

        Assert.Equal(2L * 1024 * 1024 * 1024, policy!.MaxSizeBytes);
    }

    [Fact]
    public async Task Confirmer_avant_d_avoir_envoye_renvoie_409()
    {
        var client = factory.CreateUserClient();
        var versionUrl = await client.CreateVersionUrlAsync();
        var upload = await client.StartOkAsync(versionUrl, "a.mp3", 1024);

        Assert.Equal(HttpStatusCode.Conflict, (await client.CompleteAsync(versionUrl, upload.UploadId)).StatusCode);
    }

    [Fact]
    public async Task Une_taille_differente_de_l_annonce_est_refusee()
    {
        var client = factory.CreateUserClient();
        var versionUrl = await client.CreateVersionUrlAsync();
        var upload = await client.StartOkAsync(versionUrl, "a.mp3", 2048);

        var put = await client.PutAsync(upload, TestFiles.Mp3(1024));

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task Un_exe_renomme_est_refuse_a_la_confirmation_et_efface()
    {
        var client = factory.CreateUserClient();
        var versionUrl = await client.CreateVersionUrlAsync();
        byte[] exe = [0x4D, 0x5A, 0x90, 0x00, .. new byte[1020]];
        var upload = await client.StartOkAsync(versionUrl, "maquette.mp3", exe.Length);
        await client.PutAsync(upload, exe);
        var key = await StorageKeyAsync(factory.Services, upload.UploadId);

        var complete = await client.CompleteAsync(versionUrl, upload.UploadId);

        Assert.Equal(HttpStatusCode.BadRequest, complete.StatusCode);
        Assert.Contains("ne correspond pas", await complete.Content.ReadAsStringAsync());
        var storage = factory.Services.GetRequiredService<IFileStorageService>();
        Assert.Null(await storage.GetLengthAsync(key, CancellationToken.None));
        Assert.Equal(HttpStatusCode.NotFound, (await client.CompleteAsync(versionUrl, upload.UploadId)).StatusCode);
    }

    [Fact]
    public async Task Un_autre_membre_ne_peut_ni_envoyer_ni_confirmer_l_envoi()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var member = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, member, BandRole.Member);
        var versionUrl = await owner.CreateVersionUrlAsync();
        var bytes = TestFiles.Mp3(1024);
        var upload = await owner.StartOkAsync(versionUrl, "a.mp3", bytes.Length);

        Assert.Equal(HttpStatusCode.NotFound, (await member.PutAsync(upload, bytes)).StatusCode);
        await owner.PutAsync(upload, bytes);
        Assert.Equal(HttpStatusCode.NotFound, (await member.CompleteAsync(versionUrl, upload.UploadId)).StatusCode);
    }

    [Fact]
    public async Task Un_Guest_ne_peut_pas_annoncer_d_envoi()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var guest = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, guest, BandRole.Guest);
        var versionUrl = await owner.CreateVersionUrlAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await guest.StartAsync(versionUrl, "a.mp3", 1024)).StatusCode);
    }

    [Fact]
    public async Task Un_envoi_jamais_confirme_est_efface_a_expiration()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var host = factory.WithClock(clock);
        var client = await host.CreateNewUserClientAsync();
        var versionUrl = await client.CreateVersionUrlAsync();
        var bytes = TestFiles.Mp3(1024);
        var upload = await client.StartOkAsync(versionUrl, "a.mp3", bytes.Length);
        await client.PutAsync(upload, bytes);
        var key = await StorageKeyAsync(host.Services, upload.UploadId);

        clock.Advance(FileUpload.Lifetime - TimeSpan.FromMinutes(1));
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);
        Assert.NotNull(await StorageKeyOrNullAsync(host.Services, upload.UploadId));    // pas avant l'expiration

        clock.Advance(TimeSpan.FromMinutes(1));
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);

        Assert.Null(await StorageKeyOrNullAsync(host.Services, upload.UploadId));
        var storage = host.Services.GetRequiredService<IFileStorageService>();
        Assert.Null(await storage.GetLengthAsync(key, CancellationToken.None));
        Assert.Equal(HttpStatusCode.NotFound, (await client.CompleteAsync(versionUrl, upload.UploadId)).StatusCode);
    }

    private static async Task<string> StorageKeyAsync(IServiceProvider services, Guid uploadId)
        => await StorageKeyOrNullAsync(services, uploadId) ?? throw new InvalidOperationException("Envoi introuvable.");

    private static async Task<string?> StorageKeyOrNullAsync(IServiceProvider services, Guid uploadId)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SongVaultDbContext>().FileUploads
            .Where(u => u.Id == uploadId).Select(u => u.StorageKey).SingleOrDefaultAsync();
    }
}