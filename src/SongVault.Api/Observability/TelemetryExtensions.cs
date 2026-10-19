using Azure.Monitor.OpenTelemetry.AspNetCore;

using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace SongVault.Api.Observability;

public static class TelemetryExtensions
{
    /// <summary>Variable d'environnement de la chaîne de connexion d'Application Insights (posée par main.bicep).</summary>
    public const string ConnectionStringKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    /// <summary>
    /// #18 : traces, métriques et journaux OpenTelemetry vers Application Insights (ADR 0025),
    /// seulement si la chaîne de connexion est définie. Sans elle (PC, tests, CI), rien n'est envoyé.
    /// </summary>
    public static WebApplicationBuilder AddSongVaultTelemetry(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration[ConnectionStringKey];
        if (string.IsNullOrWhiteSpace(connectionString)) return builder;

        // Les sondes de Container Apps appellent /health/* toutes les quelques secondes : ni utile ni gratuit
        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(o =>
            o.Filter = http => !http.Request.Path.StartsWithSegments("/health"));

        builder.Services.AddOpenTelemetry()
            // Avant UseAzureMonitor : le nettoyage passe avant l'export
            .WithTracing(tracing => tracing.AddProcessor(new RedactingActivityProcessor()))
            .WithLogging(logging => logging.AddProcessor(new RedactingLogProcessor()))
            .UseAzureMonitor(o =>
            {
                o.ConnectionString = connectionString;
                // Le direct du portail (Live Metrics) : inutile à notre trafic, un flux de moins qui sort
                o.EnableLiveMetrics = false;
                // Une erreur part toujours, même si sa requête n'a pas été retenue par l'échantillonnage
                o.EnableTraceBasedLogsSampler = false;
            });
        return builder;
    }
}