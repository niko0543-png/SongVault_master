using System.Threading.Channels;

using SongVault.Application.Abstractions;

namespace SongVault.Infrastructure.Email;

internal sealed class EmailQueue : IEmailQueue
{
    // Bornée : une rafale d'inscriptions ne peut pas remplir la mémoire de l'API.
    // Mode Wait par défaut : TryWrite renvoie false quand la file est pleine, sans attendre.
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(500) { SingleReader = true });

    public bool TryEnqueue(EmailMessage message) => _channel.Writer.TryWrite(message);

    public ChannelReader<EmailMessage> Reader => _channel.Reader;
}