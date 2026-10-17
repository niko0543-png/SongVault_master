namespace SongVault.Infrastructure.Antivirus;

/// <summary>Antivirus des fichiers envoyés (ADR 0024).</summary>
public sealed class AntivirusOptions
{
    public const string SectionName = "Antivirus";
    /// <summary>Aucune analyse : les fichiers sont lisibles et marqués NotScanned (avant la commercialisation).</summary>
    public const string NoneProvider = "None";
    /// <summary>Analyse par clamd (Antivirus:ClamAv), avant toute lecture du fichier.</summary>
    public const string ClamAvProvider = "ClamAv";

    public string Provider { get; set; } = NoneProvider;
}