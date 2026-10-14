namespace SongVault.Application.Notifications;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>false : rien ne part tout seul (tests d'intégration, qui lancent la préparation eux-mêmes).</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Attente entre deux lectures des nouveaux événements.</summary>
    public TimeSpan DispatchInterval { get; set; } = TimeSpan.FromMinutes(1);
    /// <summary>Événements préparés par transaction.</summary>
    public int BatchSize { get; set; } = 50;
    /// <summary>Heure d'envoi du résumé quotidien, dans DigestTimeZone.</summary>
    public int DigestHour { get; set; } = 7;
    /// <summary>Fuseau IANA du résumé ; l'heure d'été est prise en compte.</summary>
    public string DigestTimeZone { get; set; } = "Europe/Paris";
}