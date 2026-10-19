using OpenTelemetry;
using OpenTelemetry.Logs;

namespace SongVault.Api.Observability;

/// <summary>
/// Nettoie chaque journal avant son export (ADR 0025) : message, attributs et exception
/// passent par TelemetryRedactor. L'identifiant de trace du journal n'est pas touché :
/// c'est lui qui relie l'erreur à sa requête dans Application Insights.
/// </summary>
public sealed class RedactingLogProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord record)
    {
        if (record.FormattedMessage is { } message) record.FormattedMessage = TelemetryRedactor.Redact(message);
        if (record.Body is { } body) record.Body = TelemetryRedactor.Redact(body);

        if (record.Attributes is { Count: > 0 } attributes)
        {
            List<KeyValuePair<string, object?>>? copy = null;
            for (var i = 0; i < attributes.Count; i++)
            {
                var (key, value) = attributes[i];
                var redacted = TelemetryRedactor.RedactValue(value);
                if (ReferenceEquals(redacted, value)) continue;
                copy ??= [.. attributes];
                copy[i] = new(key, redacted);
            }
            if (copy is not null) record.Attributes = copy;
        }

        if (record.Exception is { } exception) record.Exception = RedactedException.Wrap(exception);
    }
}

/// <summary>
/// Remplace, dans la télémétrie seulement, une exception dont le message contient une donnée personnelle
/// (ex. une adresse dans une erreur SQL de doublon). Le type d'origine ouvre le message.
/// </summary>
public sealed class RedactedException : Exception
{
    private readonly string _details;

    private RedactedException(Exception original, string details)
        : base($"{original.GetType().FullName}: {TelemetryRedactor.Redact(original.Message)}")
        => _details = details;

    /// <summary>Pile d'appels et exceptions internes d'origine, nettoyées.</summary>
    public override string ToString() => _details;

    /// <summary>L'exception elle-même s'il n'y a rien à retirer, sinon une RedactedException.</summary>
    public static Exception Wrap(Exception exception)
    {
        var text = exception.ToString();
        var redacted = TelemetryRedactor.Redact(text);
        return ReferenceEquals(redacted, text) ? exception : new RedactedException(exception, redacted);
    }
}