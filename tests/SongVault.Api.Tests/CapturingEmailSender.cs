using System.Collections.Concurrent;

using SongVault.Application.Abstractions;

namespace SongVault.Api.Tests;

public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>Dernier e-mail envoyé à cette adresse (chaque test utilise des adresses uniques).</summary>
    public EmailMessage LastTo(string email)
        => _sent.Last(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase));


    /// <summary>
    /// Attend un e-mail pour cette adresse (et ce filtre) : ceux d'Identity partent en arrière-plan (#4),
    /// un peu après la réponse HTTP.
    /// </summary>
    public async Task<EmailMessage> WaitForAsync(string email, Func<EmailMessage, bool>? match = null)
    {
        for (var attempt = 0; attempt < 200; attempt++)            // 10 secondes au plus
        {
            var found = _sent.LastOrDefault(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase)
                                                 && (match is null || match(m)));
            if (found is not null) return found;
            await Task.Delay(50);
        }
        throw new TimeoutException($"Aucun e-mail reçu pour {email}.");
    }

    /// <summary>E-mails envoyés à cette adresse, du plus ancien au plus récent.</summary>
    public IReadOnlyList<EmailMessage> AllTo(string email)
        => [.. _sent.Where(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase))];

    /// <summary>Au moins un e-mail envoyé à cette adresse.</summary>
    public bool AnyTo(string email) => _sent.Any(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase));
}