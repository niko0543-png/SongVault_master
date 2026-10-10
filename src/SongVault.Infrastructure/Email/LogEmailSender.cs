using Microsoft.Extensions.Logging;

using SongVault.Application.Abstractions;

namespace SongVault.Infrastructure.Email;

/// <summary>N'envoie rien : écrit l'e-mail dans les journaux. Développement uniquement (le corps contient des liens secrets).</summary>
internal sealed class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        logger.LogInformation("E-mail NON envoyé (Email:Provider=Log) à {To} : {Subject}{NewLine}{Body}",
            message.To, message.Subject, Environment.NewLine, message.TextBody);
        return Task.CompletedTask;
    }
}