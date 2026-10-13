using Testcontainers.Azurite;

namespace SongVault.Api.Tests.Storage;

/// <summary>Émulateur Azure Storage, démarré une fois par classe de tests.</summary>
public sealed class AzuriteFixture : IAsyncLifetime
{
    private readonly AzuriteContainer _azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0")
        .WithCommand("--skipApiVersionCheck")       // le SDK peut être plus récent que l'émulateur
        .Build();

    public string ConnectionString => _azurite.GetConnectionString();

    public Task InitializeAsync() => _azurite.StartAsync();

    public Task DisposeAsync() => _azurite.DisposeAsync().AsTask();
}