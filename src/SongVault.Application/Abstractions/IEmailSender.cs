namespace SongVault.Application.Abstractions;

/// <summary>
/// Un e-mail prêt à partir : destinataire, sujet, corps HTML et version texte.
/// Headers : en-têtes en plus, comme List-Unsubscribe pour les notifications (#12).
/// </summary>
public sealed record EmailMessage(
    string To, string Subject, string HtmlBody, string TextBody, IReadOnlyDictionary<string, string>? Headers = null);

/// <summary>Envoi d'e-mails. Implémentation choisie par Email:Provider (Log ou Brevo, ADR 0009).</summary>
public interface IEmailSender
{
    /// <summary>Envoie l'e-mail ; lève EmailDeliveryException si le fournisseur le refuse ou est injoignable.</summary>
    Task SendAsync(EmailMessage message, CancellationToken ct);
}