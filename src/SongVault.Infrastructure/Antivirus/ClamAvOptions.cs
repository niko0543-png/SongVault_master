namespace SongVault.Infrastructure.Antivirus;

public sealed class ClamAvOptions
{
    public const string SectionName = "Antivirus:ClamAv";

    /// <summary>Hôte de clamd : « clamav » avec Docker Compose, « localhost » à côté de l'API dans Azure (conteneur voisin).</summary>
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3310;
    /// <summary>Au-delà, l'analyse est arrêtée et comptée comme un essai manqué. Moins que BackgroundJobs:StaleAfter.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);
}