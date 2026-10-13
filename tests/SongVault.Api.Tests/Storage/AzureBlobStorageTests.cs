using System.Net;
using Azure;
using SongVault.Api.Tests.Files;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SongVault.Application.Abstractions;

namespace SongVault.Api.Tests.Storage;

[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public sealed class AzureBlobStorageTests(SongVaultApiFactory factory, AzuriteFixture azurite)
    : IClassFixture<AzuriteFixture>
{
    private WebApplicationFactory<Program> BlobHost(TimeProvider? clock = null)
        => (clock is null ? factory : factory.WithClock(clock)).WithWebHostBuilder(b => b
            .UseSetting("FileStorage:Provider", "AzureBlob")
            .UseSetting("FileStorage:AzureBlob:ConnectionString", azurite.ConnectionString));

    private static string NewKey() => $"{Guid.NewGuid():N}.mp3";

    [Fact]
    public async Task Enregistre_lit_et_supprime_un_fichier()
    {
        await using var host = BlobHost();
        var storage = host.Services.GetRequiredService<IFileStorageService>();
        var key = NewKey();
        var bytes = TestFiles.Mp3(4096);

        await storage.SaveAsync(key, new MemoryStream(bytes), CancellationToken.None);

        Assert.Equal(bytes.Length, await storage.GetLengthAsync(key, CancellationToken.None));
        await using (var read = await storage.OpenReadAsync(key, CancellationToken.None))
        {
            using var copy = new MemoryStream();
            await read.CopyToAsync(copy);
            Assert.Equal(bytes, copy.ToArray());
        }

        await storage.DeleteAsync(key, CancellationToken.None);
        Assert.Null(await storage.GetLengthAsync(key, CancellationToken.None));
        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.OpenReadAsync(key, CancellationToken.None));
        await storage.DeleteAsync(key, CancellationToken.None);             // déjà effacé : sans erreur
    }

    [Fact]
    public async Task N_ecrase_jamais_un_fichier_existant()
    {
        await using var host = BlobHost();
        var storage = host.Services.GetRequiredService<IFileStorageService>();
        var key = NewKey();
        await storage.SaveAsync(key, new MemoryStream(TestFiles.Mp3(1024)), CancellationToken.None);

        var error = await Assert.ThrowsAsync<RequestFailedException>(
            () => storage.SaveAsync(key, new MemoryStream(TestFiles.Mp3(1024)), CancellationToken.None));

        Assert.Equal(409, error.Status);
    }

    [Fact]
    public async Task L_URL_d_envoi_cree_le_fichier_sans_permettre_de_le_lire()
    {
        await using var host = BlobHost();
        var storage = host.Services.GetRequiredService<IFileStorageService>();
        var key = NewKey();
        var bytes = TestFiles.Mp3(2048);

        var upload = storage.CreateUploadUrl(key, DateTimeOffset.UtcNow.AddMinutes(5))!;
        var put = await StorageHttp.PutAsync(upload, bytes);

        Assert.Equal(HttpStatusCode.Created, put.StatusCode);
        Assert.Equal(bytes.Length, await storage.GetLengthAsync(key, CancellationToken.None));
        Assert.False((await StorageHttp.Client.GetAsync(upload.Url)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Le_conteneur_est_prive_et_l_URL_de_lecture_impose_type_et_nom()
    {
        await using var host = BlobHost();
        var storage = host.Services.GetRequiredService<IFileStorageService>();
        var key = NewKey();
        var bytes = TestFiles.Mp3(2048);
        await storage.SaveAsync(key, new MemoryStream(bytes), CancellationToken.None);

        var url = storage.CreateDownloadUrl(key, "audio/mpeg", "Maquette été.mp3")!;
        var withoutSignature = new UriBuilder(url) { Query = "" }.Uri;

        Assert.False((await StorageHttp.Client.GetAsync(withoutSignature)).IsSuccessStatusCode);
        var response = await StorageHttp.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("audio/mpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    public async Task Une_URL_de_lecture_expiree_est_refusee()
    {
        // Horloge de l'API 31 minutes en arrière : l'URL (30 minutes) est déjà expirée pour le stockage
        await using var host = BlobHost(new TestClock(DateTimeOffset.UtcNow.AddMinutes(-31)));
        var storage = host.Services.GetRequiredService<IFileStorageService>();
        var key = NewKey();
        await storage.SaveAsync(key, new MemoryStream(TestFiles.Mp3(1024)), CancellationToken.None);

        var response = await StorageHttp.Client.GetAsync(storage.CreateDownloadUrl(key, "audio/mpeg", null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Fact]
    public async Task En_AzureBlob_le_contenu_ne_passe_jamais_par_l_API()
    {
        await using var host = BlobHost();
        var client = await host.CreateNewUserClientAsync();
        var versionUrl = await client.CreateVersionUrlAsync();
        var bytes = TestFiles.Mp3(512 * 1024);

        // Envoi : l'API donne une URL signée, le fichier part vers le stockage
        var upload = await client.StartOkAsync(versionUrl, "Studio été.mp3", bytes.Length);
        Assert.Contains("sig=", upload.UploadUrl);
        Assert.Equal("BlockBlob", upload.Headers["x-ms-blob-type"]);
        Assert.Equal(HttpStatusCode.Created, (await client.PutAsync(upload, bytes)).StatusCode);
        var complete = await client.CompleteAsync(versionUrl, upload.UploadId);
        Assert.Equal(HttpStatusCode.Created, complete.StatusCode);

        // Lecture : l'API vérifie l'accès, puis redirige vers une URL signée
        var noRedirect = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        noRedirect.DefaultRequestHeaders.Add("Cookie", client.DefaultRequestHeaders.GetValues("Cookie"));
        var response = await noRedirect.GetAsync($"{complete.Headers.Location}?download=true");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var file = await StorageHttp.Client.GetAsync(response.Headers.Location);
        Assert.Equal(bytes, await file.Content.ReadAsByteArrayAsync());
        Assert.Equal("Studio été.mp3", file.Content.Headers.ContentDisposition?.FileNameStar);
    }
}