using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SongVault.Application.Abstractions;

namespace SongVault.Infrastructure.Email;

/// <summary>Vide la file d'e-mails et confie chacun à IEmailSender, dans l'ordre d'arrivée.</summary>
internal sealed class EmailQueueWorker(EmailQueue queue, IServiceScopeFactory scopes, ILogger<EmailQueueWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IEmailSender>().SendAsync(message, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Un délai dépassé côté HttpClient lève aussi une OperationCanceledException : seul l'arrêt de l'API la laisse passer.
                // #18 : jamais l'adresse du destinataire dans les journaux (ADR 0025)
                logger.LogError(ex, "E-mail « {Subject} » non envoyé.", message.Subject);
            }
        }
    }
}