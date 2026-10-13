using System.Net.Http.Headers;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;

namespace SongVault.Infrastructure.Storage;

/// <summary>Fichiers dans un conteneur privé Azure Blob Storage (ADR 0012).</summary>
internal sealed class AzureBlobFileStorageService : IFileStorageService
{
    // Exigé par l'opération Put Blob : le navigateur doit l'envoyer avec son PUT
    private static readonly IReadOnlyDictionary<string, string> UploadHeaders =
        new Dictionary<string, string> { ["x-ms-blob-type"] = "BlockBlob" };

    private readonly BlobContainerClient _container;
    private readonly AzureBlobStorageOptions _options;
    private readonly Uri? _publicEndpoint;
    private readonly TimeProvider _clock;

    public AzureBlobFileStorageService(IOptions<FileStorageOptions> options, TimeProvider clock)
    {
        _options = options.Value.AzureBlob;
        _container = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
        _publicEndpoint = string.IsNullOrWhiteSpace(_options.PublicEndpoint) ? null : new Uri(_options.PublicEndpoint);
        _clock = clock;
    }

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken ct)
    {
        // If-None-Match: * : refusé (409) si le fichier existe déjà, comme FileMode.CreateNew en local
        await Blob(storageKey).UploadAsync(content,
            new BlobUploadOptions { Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All } }, ct);
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        try
        {
            return await Blob(storageKey).OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new FileNotFoundException("Fichier absent du stockage.", storageKey, ex);
        }
    }

    public async Task DeleteAsync(string storageKey, CancellationToken ct)
        => await Blob(storageKey).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);

    public async Task<long?> GetLengthAsync(string storageKey, CancellationToken ct)
    {
        try
        {
            return (await Blob(storageKey).GetPropertiesAsync(cancellationToken: ct)).Value.ContentLength;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public DirectUpload? CreateUploadUrl(string storageKey, DateTimeOffset expiresAt)
    {
        // Créer ce seul fichier (sp=c) : ni lecture, ni liste du conteneur
        var url = Blob(storageKey).GenerateSasUri(BlobSasPermissions.Create, expiresAt);
        return new DirectUpload(ForBrowser(url), UploadHeaders);
    }

    public Uri? CreateDownloadUrl(string storageKey, string contentType, string? attachmentName)
    {
        var sas = new BlobSasBuilder(BlobSasPermissions.Read, _clock.GetUtcNow() + _options.DownloadUrlLifetime)
        {
            BlobContainerName = _container.Name,
            BlobName = storageKey,
            Resource = "b",
            // Le stockage répondra avec ce type et ce nom, quels que soient ceux envoyés par le navigateur
            ContentType = contentType,
            ContentDisposition = attachmentName is null
                ? "inline"
                : new ContentDispositionHeaderValue("attachment") { FileNameStar = attachmentName }.ToString(),
        };
        return ForBrowser(Blob(storageKey).GenerateSasUri(sas));
    }

    private BlobClient Blob(string storageKey) => _container.GetBlobClient(storageKey);

    // Azurite dans Docker : l'API l'appelle sur http://azurite:10000, le navigateur sur http://localhost:10000.
    // La signature ne couvre pas l'hôte : on peut le remplacer.
    private Uri ForBrowser(Uri url) => _publicEndpoint is null
        ? url
        : new UriBuilder(url) { Scheme = _publicEndpoint.Scheme, Host = _publicEndpoint.Host, Port = _publicEndpoint.Port }.Uri;
}