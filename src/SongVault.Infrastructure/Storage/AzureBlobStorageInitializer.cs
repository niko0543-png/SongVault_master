using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SongVault.Infrastructure.Storage;

/// <summary>Au démarrage, avec AzureBlob : crée le conteneur privé s'il manque et règle le CORS si CorsOrigin est donné.</summary>
internal sealed class AzureBlobStorageInitializer(IOptions<FileStorageOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.Value.Provider != FileStorageOptions.AzureBlobProvider) return;
        var blob = options.Value.AzureBlob;

        var service = new BlobServiceClient(blob.ConnectionString);
        await service.GetBlobContainerClient(blob.ContainerName)
            .CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(blob.CorsOrigin)) return;
        var properties = (await service.GetPropertiesAsync(cancellationToken)).Value;
        properties.Cors =
        [
            new BlobCorsRule
            {
                AllowedOrigins = blob.CorsOrigin,
                AllowedMethods = "GET,HEAD,PUT",
                AllowedHeaders = "*",
                ExposedHeaders = "*",
                MaxAgeInSeconds = 3600,
            },
        ];
        await service.SetPropertiesAsync(properties, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}