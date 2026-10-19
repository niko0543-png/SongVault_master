using Microsoft.Extensions.Diagnostics.HealthChecks;

using SongVault.Api.Contracts.ServiceStatus;

namespace SongVault.Api.ServiceStatus;

/// <summary>
/// #18 : état du service pour la page de statut, d'après les contrôles de santé « ready » et « status ».
/// Gardé 30 secondes : une page publique ne doit pas pouvoir charger la base.
/// </summary>
public sealed class StatusReporter(HealthCheckService health, TimeProvider clock)
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    // Nom du contrôle de santé → nom affiché ; dans l'ordre de la page
    private static readonly (string Check, string Name)[] Components =
    [
        ("database", "Base de données"),
        ("jobs", "Tâches de fond (e-mails, analyses, aperçus)"),
    ];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private StatusResponse? _last;

    public async Task<StatusResponse> GetAsync(CancellationToken ct)
    {
        if (_last is { } cached && IsFresh(cached)) return cached;
        await _gate.WaitAsync(ct);
        try
        {
            if (_last is { } again && IsFresh(again)) return again;     // calculé pendant l'attente
            var report = await health.CheckHealthAsync(c => c.Tags.Contains("ready") || c.Tags.Contains("status"), ct);
            return _last = From(report, clock.GetUtcNow());
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Le rapport des contrôles, traduit pour un visiteur. L'application a répondu : elle est toujours « ok ».</summary>
    public static StatusResponse From(HealthReport report, DateTimeOffset checkedAt)
    {
        List<ComponentStatusResponse> components = [new("Application", "ok")];
        foreach (var (check, name) in Components)
            if (report.Entries.TryGetValue(check, out var entry))
                components.Add(new(name, Text(entry.Status)));
        return new StatusResponse(Text(report.Status), checkedAt, components);
    }

    private static string Text(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "ok",
        HealthStatus.Degraded => "degraded",
        _ => "down",
    };

    private bool IsFresh(StatusResponse last) => clock.GetUtcNow() - last.CheckedAt < CacheDuration;
}