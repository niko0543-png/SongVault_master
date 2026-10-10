namespace SongVault.Application.Abstractions;

/// <summary>Un e-mail prêt à partir : destinataire, sujet, corps HTML et version texte.</summary>
public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody);

/// <summary>Envoi d'e-mails. Implémentation choisie par Email:Provider (Log ou Brevo, ADR 0009).</summary>
public interface IEmailSender
{
    /// <summary>Envoie l'e-mail ; lève EmailDeliveryException si le fournisseur le refuse ou est injoignable.</summary>
    Task SendAsync(EmailMessage message, CancellationToken ct);
}