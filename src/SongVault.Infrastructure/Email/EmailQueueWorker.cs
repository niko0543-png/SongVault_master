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
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Pas de nouvel essai : l'utilisateur peut redemander l'e-mail depuis l'écran.
                // Une exception non rattrapée arrêterait le service, et avec lui toute l'API.
                logger.LogError(ex, "E-mail « {Subject} » non envoyé à {To}.", message.Subject, message.To);
            }
        }
    }
}