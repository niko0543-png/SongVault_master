namespace SongVault.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Chemin absolu, ou relatif au dossier du projet API (ContentRoot).</summary>
    public string RootPath { get; set; } = "";
}