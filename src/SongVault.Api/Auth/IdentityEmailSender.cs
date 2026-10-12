using System.Net;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Application.Accounts;
using SongVault.Application.Common;

namespace SongVault.Api.Auth;

/// <summary>
/// E-mails des endpoints d'Identity (MapIdentityApi), en français et avec des liens vers le front.
/// Ils passent par la file : la réponse n'attend pas le fournisseur, et « mot de passe oublié »
/// répond dans le même temps pour un compte connu ou inconnu (ADR 0010).
/// </summary>
public sealed class IdentityEmailSender(IEmailQueue queue, IOptions<AppOptions> app, ILogger<IdentityEmailSender> logger)
    : IEmailSender<IdentityUser>
{
    private string PublicUrl => app.Value.PublicUrl.TrimEnd('/');

    /// <summary>Lien d'Identity : /api/auth/confirmEmail?userId=…&amp;code=… ; sa query est reprise sur /confirm-email.</summary>
    public Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink)
    {
        var query = new Uri(WebUtility.HtmlDecode(confirmationLink)).Query;      // « ?userId=…&code=… »
        Enqueue(AccountEmails.ConfirmEmail(email, $"{PublicUrl}/confirm-email{query}", AccountTokens.EmailConfirmationLifetime));
        return Task.CompletedTask;
    }

    /// <summary>Appelé par /forgotPassword avec un code Base64Url, seulement pour un compte confirmé.</summary>
    public Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode)
    {
        var code = WebUtility.HtmlDecode(resetCode);
        var link = $"{PublicUrl}/reset-password?email={Uri.EscapeDataString(email)}&code={Uri.EscapeDataString(code)}";
        Enqueue(AccountEmails.ResetPassword(email, link, AccountTokens.PasswordResetLifetime));
        return Task.CompletedTask;
    }

    /// <summary>Pas appelé par MapIdentityApi (qui envoie un code), mais exigé par l'interface.</summary>
    public Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink)
    {
        Enqueue(AccountEmails.ResetPassword(email, WebUtility.HtmlDecode(resetLink), AccountTokens.PasswordResetLifetime));
        return Task.CompletedTask;
    }

    private void Enqueue(EmailMessage message)
    {
        if (!queue.TryEnqueue(message))
            logger.LogError("File d'e-mails pleine : « {Subject} » pour {To} n'est pas parti.", message.Subject, message.To);
    }
}