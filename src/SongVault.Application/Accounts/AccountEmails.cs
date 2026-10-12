using System.Net;

using SongVault.Application.Abstractions;

namespace SongVault.Application.Accounts;

/// <summary>E-mails du compte (ADR 0010) : confirmation de l'adresse et réinitialisation du mot de passe.</summary>
public static class AccountEmails
{
    public static EmailMessage ConfirmEmail(string to, string link, TimeSpan validFor)
    {
        var duration = Duration(validFor);
        var text = $"""
            Bonjour,

            Confirmez votre adresse pour activer votre compte SongVault : {link}

            Ce lien est valable {duration}. Si vous n'avez pas créé de compte, ignorez ce message.
            """;
        var html = $"""
            <p>Bonjour,</p>
            <p><a href="{H(link)}">Confirmer mon adresse</a> pour activer votre compte SongVault.</p>
            <p style="color:#666">Ce lien est valable {duration}. Si vous n'avez pas créé de compte, ignorez ce message.</p>
            """;
        return new EmailMessage(to, "Confirmez votre adresse e-mail", html, text);
    }

    public static EmailMessage ResetPassword(string to, string link, TimeSpan validFor)
    {
        var duration = Duration(validFor);
        var text = $"""
            Bonjour,

            Pour choisir un nouveau mot de passe SongVault, ouvrez ce lien : {link}

            Il est valable {duration} et ne sert qu'une fois. Si vous n'avez rien demandé, ignorez ce message : votre mot de passe ne change pas.
            """;
        var html = $"""
            <p>Bonjour,</p>
            <p><a href="{H(link)}">Choisir un nouveau mot de passe</a></p>
            <p style="color:#666">Ce lien est valable {duration} et ne sert qu'une fois. Si vous n'avez rien demandé, ignorez ce message : votre mot de passe ne change pas.</p>
            """;
        return new EmailMessage(to, "Réinitialisation de votre mot de passe", html, text);
    }

    /// <summary>2 h donne « 2 heures », 24 h donne « 24 heures ».</summary>
    private static string Duration(TimeSpan validFor)
        => validFor.TotalHours == 1 ? "1 heure" : $"{validFor.TotalHours:0} heures";

    private static string H(string value) => WebUtility.HtmlEncode(value);
}