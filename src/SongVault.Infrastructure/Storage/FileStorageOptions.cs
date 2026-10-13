namespace SongVault.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";
    public const string LocalProvider = "Local";
    public const string AzureBlobProvider = "AzureBlob";

    /// <summary>Local (volume, par défaut) ou AzureBlob (ADR 0012). Lu au démarrage.</summary>
    public string Provider { get; set; } = LocalProvider;

    /// <summary>Stockage Local : chemin absolu, ou relatif au dossier du projet API (ContentRoot).</summary>
    public string RootPath { get; set; } = "";

    public AzureBlobStorageOptions AzureBlob { get; set; } = new();
}

public sealed class AzureBlobStorageOptions
{
    /// <summary>Chaîne de connexion du compte, avec sa clé : elle signe les URL. Secret, jamais dans le dépôt.</summary>
    public string ConnectionString { get; set; } = "";

    public string ContainerName { get; set; } = "songvault-files";

    /// <summary>Adresse du stockage vue par le navigateur, si elle diffère de celle de la chaîne (Azurite dans Docker).</summary>
    public string? PublicEndpoint { get; set; }

    /// <summary>Origine du site autorisée en CORS, réglée par l'API au démarrage (Azurite). Sur Azure, Bicep s'en charge.</summary>
    public string? CorsOrigin { get; set; }

    /// <summary>Durée de validité d'une URL de lecture.</summary>
    public TimeSpan DownloadUrlLifetime { get; set; } = TimeSpan.FromMinutes(30);
}