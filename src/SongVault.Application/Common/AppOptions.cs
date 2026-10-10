namespace SongVault.Application.Common;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Adresse du front vue par les utilisateurs, sans / final (ex. http://localhost:8080).</summary>
    public string PublicUrl { get; set; } = "";
}