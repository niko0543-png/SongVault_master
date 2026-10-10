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
}