namespace SongVault.Application.Abstractions;

/// <summary>Programme une tâche de fond (ADR 0011).</summary>
public interface IBackgroundJobs
{
    /// <summary>
    /// Ajoute la tâche au contexte, sans l'enregistrer : elle part avec le prochain SaveChangesAsync,
    /// dans la même transaction que la donnée qui la motive.
    /// </summary>
    void Enqueue(string type, string payload, DateTimeOffset runAfter);

    /// <summary>Une tâche de ce type attend son heure (Pending). Une tâche en cours ne compte pas (#12).</summary>
    Task<bool> HasPendingAsync(string type, CancellationToken ct);
}

/// <summary>Ce que reçoit un gestionnaire : la tâche, et s'il s'agit du dernier essai.</summary>
public sealed record BackgroundJobContext(Guid JobId, string Payload, int Attempt, bool IsLastAttempt);

/// <summary>Exécute un type de tâche. Une exception fait réessayer la tâche plus tard (trois essais au total).</summary>
public interface IBackgroundJobHandler
{
    string Type { get; }
    Task RunAsync(BackgroundJobContext job, CancellationToken ct);
}