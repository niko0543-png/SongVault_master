using System.Net.Http.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;

namespace SongVault.Infrastructure.Email;

/// <summary>Envoi par l'API HTTP de Brevo (e-mails transactionnels).</summary>
internal sealed class BrevoEmailSender(HttpClient http, IOptions<EmailOptions> options, ILogger<BrevoEmailSender> logger)
    : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var o = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email")
        {
            Content = JsonContent.Create(new
            {
                sender = new { name = o.FromName, email = o.FromAddress },
                to = new[] { new { email = message.To } },
                subject = message.Subject,
                htmlContent = message.HtmlBody,
                textContent = message.TextBody,
            }),
        };
        request.Headers.Add("api-key", o.Brevo.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Brevo injoignable");
            throw new EmailDeliveryException("Le service d'e-mail est injoignable.", ex);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode) return;
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Brevo a refusé l'e-mail ({Status}) : {Body}", (int)response.StatusCode, body);
            throw new EmailDeliveryException($"Le service d'e-mail a répondu {(int)response.StatusCode}.");
        }
    }
}