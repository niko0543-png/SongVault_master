using System.Globalization;
using System.Net;

using SongVault.Application.Abstractions;
using SongVault.Domain.Bands;

namespace SongVault.Application.Invitations;

public static class InvitationEmail
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static EmailMessage Build(
        string to, string bandName, string inviter, BandRole role, string link, DateTimeOffset expiresAt)
    {
        var roleLabel = role == BandRole.Guest ? "invité (écoute seulement)" : "membre";
        var expires = expiresAt.ToString("d MMMM yyyy", French);
        var subject = $"Invitation à rejoindre « {bandName} » sur SongVault";

        var text = $"""
            {inviter} vous invite à rejoindre le groupe « {bandName} » sur SongVault, en tant que {roleLabel}.

            Ouvrir l'invitation : {link}

            Ce lien ne sert qu'une fois et expire le {expires}. Si vous ne connaissez pas l'expéditeur, ignorez ce message.
            """;

        Func<string, string> h = s => WebUtility.HtmlEncode(s);   // lambda : HtmlEncode a deux surcharges et renvoie string?
        var html = $"""
            <p>{h(inviter)} vous invite à rejoindre le groupe « {h(bandName)} » sur SongVault, en tant que {h(roleLabel)}.</p>
            <p><a href="{h(link)}">Ouvrir l'invitation</a></p>
            <p style="color:#666">Ce lien ne sert qu'une fois et expire le {h(expires)}. Si vous ne connaissez pas l'expéditeur, ignorez ce message.</p>
            """;

        return new EmailMessage(to, subject, html, text);
    }
}