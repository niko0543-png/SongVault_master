namespace SongVault.Application.Abstractions;

/// <summary>
/// File d'envoi en mémoire (ADR 0010) : l'e-mail part après la réponse HTTP, par IEmailSender.
/// Un e-mail encore dans la file est perdu si l'API s'arrête.
/// </summary>
public interface IEmailQueue
{
    /// <summary>Dépose l'e-mail ; false si la file est pleine (il ne partira pas).</summary>
    bool TryEnqueue(EmailMessage message);
}