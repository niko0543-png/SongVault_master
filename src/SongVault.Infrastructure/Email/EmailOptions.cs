namespace SongVault.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public const string LogProvider = "Log";
    public const string BrevoProvider = "Brevo";

    /// <summary>Log (défaut : écrit l'e-mail dans les journaux) ou Brevo.</summary>
    public string Provider { get; set; } = LogProvider;
    public string FromAddress { get; set; } = "no-reply@songvault.local";
    public string FromName { get; set; } = "SongVault";
    public BrevoOptions Brevo { get; set; } = new();
}

public sealed class BrevoOptions
{
    /// <summary>Clé d'API Brevo (Paramètres > SMTP et API > Clés API). Secret : jamais dans appsettings.json.</summary>
    public string ApiKey { get; set; } = "";
}