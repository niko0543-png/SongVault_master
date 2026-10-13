namespace SongVault.Infrastructure.Jobs;

public enum BackgroundJobStatus { Pending, Running, Succeeded, Failed }

/// <summary>Une tâche de fond (ADR 0011). Modifiée seulement par BackgroundJobRunner, en SQL direct.</summary>
public sealed class BackgroundJob
{
    public const int TypeMaxLength = 100;
    public const int ErrorMaxLength = 2000;

    public Guid Id { get; set; }
    public string Type { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public BackgroundJobStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset RunAfter { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Error { get; set; }
}