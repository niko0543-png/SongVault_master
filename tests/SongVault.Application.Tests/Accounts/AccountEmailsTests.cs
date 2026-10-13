using SongVault.Application.Accounts;

namespace SongVault.Application.Tests.Accounts;

public sealed class AccountEmailsTests
{
    [Fact]
    public void Confirmation_donne_le_lien_et_sa_duree()
    {
        var message = AccountEmails.ConfirmEmail(
            "a@b.c", "http://localhost:8080/confirm-email?userId=u&code=x", TimeSpan.FromHours(24));

        Assert.Equal("a@b.c", message.To);
        Assert.Contains("http://localhost:8080/confirm-email?userId=u&code=x", message.TextBody);
        Assert.Contains("href=\"http://localhost:8080/confirm-email?userId=u&amp;code=x\"", message.HtmlBody);
        Assert.Contains("24 heures", message.TextBody);
    }

    [Fact]
    public void Reinitialisation_indique_la_duree_de_2_heures()
        => Assert.Contains("2 heures",
            AccountEmails.ResetPassword("a@b.c", "http://localhost:8080/reset-password?email=a&code=c", TimeSpan.FromHours(2)).TextBody);


    [Fact]
    public void Archive_prete_donne_la_date_d_expiration_sans_lien_de_telechargement()
    {
        var message = AccountEmails.ExportReady(
            "a@b.c", "http://localhost:8080/account", new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal("Vos données SongVault sont prêtes", message.Subject);
        Assert.Contains("http://localhost:8080/account", message.TextBody);
        Assert.Contains("19 octobre 2026", message.TextBody);
        Assert.DoesNotContain("/api/", message.TextBody);
    }
}