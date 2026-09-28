using SongVault.Api.Tests;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<SongVaultApiFactory>
{
    public const string Name = "api";
}