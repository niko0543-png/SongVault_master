using System.Text.Json;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using SongVault.Api.ServiceStatus;

namespace SongVault.Api.Tests.ServiceStatus;

/// <summary>#18 : la page de statut, traduite des contrôles de santé, sans détail technique.</summary>
public sealed class StatusReporterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static HealthReport Report(HealthStatus database, HealthStatus jobs, Exception? error = null)
        => new(new Dictionary<string, HealthReportEntry>
        {
            ["database"] = new(database, error?.Message, TimeSpan.Zero, error, null),
            ["jobs"] = new(jobs, null, TimeSpan.Zero, null, null),
        }, TimeSpan.Zero);

    [Fact]
    public void Tout_va_bien()
    {
        var status = StatusReporter.From(Report(HealthStatus.Healthy, HealthStatus.Healthy), Now);

        Assert.Equal("ok", status.Status);
        Assert.Equal(Now, status.CheckedAt);
        string[] names = ["Application", "Base de données", "Tâches de fond (e-mails, analyses, aperçus)"];
        Assert.Equal(names, status.Components.Select(c => c.Name));
        Assert.All(status.Components, c => Assert.Equal("ok", c.Status));
    }

    [Fact]
    public void Des_taches_en_retard_rendent_le_service_degrade()
    {
        var status = StatusReporter.From(Report(HealthStatus.Healthy, HealthStatus.Degraded), Now);

        Assert.Equal("degraded", status.Status);
        Assert.Equal("degraded", status.Components[2].Status);
    }

    [Fact]
    public void Base_injoignable_service_en_panne_sans_le_message_d_erreur()
    {
        var status = StatusReporter.From(
            Report(HealthStatus.Unhealthy, HealthStatus.Degraded, new InvalidOperationException("Login failed for user 'songvaultadmin'")), Now);

        Assert.Equal("down", status.Status);
        Assert.Equal("down", status.Components[1].Status);
        Assert.DoesNotContain("songvaultadmin", JsonSerializer.Serialize(status));
    }

    [Fact]
    public async Task Les_controles_ne_sont_refaits_qu_apres_30_secondes()
    {
        var health = new CountingHealthCheckService();
        var clock = new TestClock(Now);
        var reporter = new StatusReporter(health, clock);

        await reporter.GetAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(29));
        await reporter.GetAsync(CancellationToken.None);
        Assert.Equal(1, health.Calls);

        clock.Advance(TimeSpan.FromSeconds(2));
        var status = await reporter.GetAsync(CancellationToken.None);
        Assert.Equal(2, health.Calls);
        Assert.Equal(Now.AddSeconds(31), status.CheckedAt);
    }

    private sealed class CountingHealthCheckService : HealthCheckService
    {
        public int Calls { get; private set; }

        public override Task<HealthReport> CheckHealthAsync(
            Func<HealthCheckRegistration, bool>? predicate, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Report(HealthStatus.Healthy, HealthStatus.Healthy));
        }
    }
}