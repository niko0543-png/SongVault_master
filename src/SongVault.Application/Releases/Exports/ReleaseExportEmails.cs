using System.Globalization;
using System.Net;

using SongVault.Application.Abstractions;

namespace SongVault.Application.Releases.Exports;

public static class ReleaseExportEmails
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    /// <summary>
    /// L'archive est prête. Pas de lien de téléchargement dans l'e-mail : on se connecte pour la récupérer
    /// (ADR 0011, repris par l'ADR 0031).
    /// </summary>
    public static EmailMessage Ready(string to, string releaseTitle, string releaseLink, DateTimeOffset expiresAt)
    {
        var expires = TimeZoneInfo.ConvertTime(expiresAt, Paris)
            .ToString("d MMMM yyyy 'à' HH'h'mm", CultureInfo.GetCultureInfo("fr-FR"));
        var text = $"""
            Bonjour,

            L'archive de la sortie « {releaseTitle} » est prête : fichiers audio, paroles, crédits et pochette.
            Téléchargez-la depuis la page de la sortie : {releaseLink}

            Elle reste disponible jusqu'au {expires} (heure de Paris).
            """;
        var html = $"""
            <p>Bonjour,</p>
            <p>L'archive de la sortie « {H(releaseTitle)} » est prête : fichiers audio, paroles, crédits et pochette.
            <a href="{H(releaseLink)}">Téléchargez-la depuis la page de la sortie</a>.</p>
            <p style="color:#666">Elle reste disponible jusqu'au {H(expires)} (heure de Paris).</p>
            """;
        return new EmailMessage(to, $"Archive prête : {releaseTitle}", html, text);
    }

    private static string H(string value) => WebUtility.HtmlEncode(value);
}