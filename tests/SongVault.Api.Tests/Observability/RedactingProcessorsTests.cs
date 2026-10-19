using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Logs;

using SongVault.Api.Observability;

namespace SongVault.Api.Tests.Observability;

/// <summary>#18 : traces et journaux nettoyés avant l'export, le journal restant relié à sa trace.</summary>
[Trait("Category", "Security")]
public sealed class RedactingProcessorsTests
{
    [Fact]
    public void Une_trace_part_sans_jeton_ni_navigateur()
    {
        using var activity = new System.Diagnostics.Activity("GET api/share/{token}");
        activity.SetTag("url.path", "/api/share/SecretToken/files/42/content");
        activity.SetTag("url.query", "?token=Redacted");
        activity.SetTag("user_agent.original", "Mozilla/5.0");
        activity.SetTag("http.response.status_code", 500);

        new RedactingActivityProcessor().OnEnd(activity);

        Assert.Equal("/api/share/{token}/files/42/content", activity.GetTagItem("url.path"));
        Assert.Equal("?token=Redacted", activity.GetTagItem("url.query"));
        Assert.Null(activity.GetTagItem("user_agent.original"));
        Assert.Equal(500, activity.GetTagItem("http.response.status_code"));
    }

    [Fact]
    public void Un_journal_d_erreur_part_sans_donnee_personnelle_et_relie_a_sa_trace()
    {
        var captured = new CapturingLogProcessor();
        using (var factory = LoggerFactory.Create(logging => logging.AddOpenTelemetry(o =>
        {
            o.IncludeFormattedMessage = true;
            o.AddProcessor(new RedactingLogProcessor());
            o.AddProcessor(captured);                    // après le nettoyage, comme l'export
        })))
        {
            using var request = new System.Diagnostics.Activity("requête").Start();
            factory.CreateLogger("SongVault.Test").LogError(
                new InvalidOperationException("Doublon : niko@example.com existe déjà."),
                "E-mail pour {To} sur {Path}", "jean@example.com", new PathString("/api/share/SecretToken"));
            Assert.Equal(request.TraceId, captured.TraceId);
        }

        var everything = string.Join(" | ", new[] { captured.Message, captured.ExceptionText }.Concat(captured.Attributes));
        Assert.DoesNotContain("@example.com", everything);
        Assert.DoesNotContain("SecretToken", everything);
        Assert.Equal("E-mail pour [e-mail] sur /api/share/{token}", captured.Message);
        Assert.StartsWith("System.InvalidOperationException: Doublon : [e-mail]", captured.ExceptionMessage);
    }

    /// <summary>Copie ce qu'aurait exporté Azure Monitor : un LogRecord est réutilisé après OnEnd.</summary>
    private sealed class CapturingLogProcessor : BaseProcessor<LogRecord>
    {
        public string? Message { get; private set; }
        public List<string> Attributes { get; } = [];
        public string? ExceptionMessage { get; private set; }
        public string? ExceptionText { get; private set; }
        public ActivityTraceId TraceId { get; private set; }

        public override void OnEnd(LogRecord record)
        {
            Message = record.FormattedMessage;
            Attributes.AddRange(record.Attributes?.Select(a => $"{a.Key}={a.Value}") ?? []);
            ExceptionMessage = record.Exception?.Message;
            ExceptionText = record.Exception?.ToString();
            TraceId = record.TraceId;
        }
    }
}