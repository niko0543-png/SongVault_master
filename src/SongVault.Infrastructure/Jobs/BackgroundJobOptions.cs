namespace SongVault.Infrastructure.Jobs;

public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";

    /// <summary>false : le service d'arrière-plan ne démarre pas (tests d'intégration, qui lancent les tâches eux-mêmes).</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Attente quand il n'y a rien à faire.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>Essais avant de passer la tâche en Failed.</summary>
    public int MaxAttempts { get; set; } = 3;
    /// <summary>Une tâche en cours depuis plus longtemps est reprise (API arrêtée pendant l'exécution).</summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(30);
}