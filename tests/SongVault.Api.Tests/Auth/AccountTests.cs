using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace SongVault.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public sealed partial class AccountTests(SongVaultApiFactory factory)
{
    private const string NewPassword = "Nouveau-Password2!";

    private static string NewEmail() => $"account-{Guid.NewGuid():N}@test.local";

    private HttpClient Anonymous() => factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email)
        => client.PostAsJsonAsync("/api/auth/register", new { email, password = SongVaultApiFactory.TestPassword });

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password = SongVaultApiFactory.TestPassword)
        => client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password });

    private static Task<HttpResponseMessage> ForgotAsync(HttpClient client, string email)
        => client.PostAsJsonAsync("/api/auth/forgotPassword", new { email });

    /// <summary>Query du lien de confirmation reçu : « ?userId=…&amp;code=… ».</summary>
    private async Task<string> ConfirmationQueryAsync(string email)
    {
        var message = await factory.Emails.WaitForAsync(email, m => m.TextBody.Contains("/confirm-email?"));
        return ConfirmLink().Match(message.TextBody).Groups[1].Value;
    }

    /// <summary>Code du lien de réinitialisation reçu.</summary>
    private static async Task<string> ResetCodeAsync(CapturingEmailSender emails, string email)
    {
        var message = await emails.WaitForAsync(email, m => m.TextBody.Contains("/reset-password?"));
        return Uri.UnescapeDataString(ResetLink().Match(message.TextBody).Groups[1].Value);
    }

    [GeneratedRegex(@"/confirm-email(\?\S+)")]
    private static partial Regex ConfirmLink();

    [GeneratedRegex(@"/reset-password\?email=[^&\s]+&code=([^&\s]+)")]
    private static partial Regex ResetLink();

    [Fact]
    public async Task Inscription_envoie_un_lien_de_confirmation_vers_le_front()
    {
        var email = NewEmail();

        Assert.Equal(HttpStatusCode.OK, (await RegisterAsync(Anonymous(), email)).StatusCode);

        var message = await factory.Emails.WaitForAsync(email);
        Assert.Equal("Confirmez votre adresse e-mail", message.Subject);
        Assert.Contains("http://localhost:8080/confirm-email?userId=", message.TextBody);
        Assert.Contains("&code=", message.TextBody);
    }

    [Fact]
    public async Task Connexion_refusee_tant_que_l_adresse_n_est_pas_confirmee()
    {
        var client = Anonymous();
        var email = NewEmail();
        await RegisterAsync(client, email);

        var login = await LoginAsync(client, email);

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal("NotAllowed", (await login.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
    }

    [Fact]
    public async Task Le_lien_de_confirmation_permet_de_se_connecter()
    {
        var client = Anonymous();
        var email = NewEmail();
        await RegisterAsync(client, email);

        var confirm = await client.GetAsync($"/api/auth/confirmEmail{await ConfirmationQueryAsync(email)}");

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, email)).StatusCode);
    }

    [Fact]
    public async Task Un_lien_de_confirmation_modifie_est_refuse()
    {
        var client = Anonymous();
        var email = NewEmail();
        await RegisterAsync(client, email);
        var query = await ConfirmationQueryAsync(email);

        var confirm = await client.GetAsync($"/api/auth/confirmEmail{query}AAAA");    // le code est en dernier

        Assert.Equal(HttpStatusCode.Unauthorized, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, email)).StatusCode);
    }

    [Fact]
    public async Task Mot_de_passe_oublie_repond_pareil_pour_une_adresse_connue_ou_inconnue()
    {
        var known = NewEmail();
        await factory.CreateNewUserClientAsync(known);
        var unknown = NewEmail();
        var client = Anonymous();

        var forUnknown = await ForgotAsync(client, unknown);
        var forKnown = await ForgotAsync(client, known);

        Assert.Equal(HttpStatusCode.OK, forUnknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, forKnown.StatusCode);
        Assert.Equal(await forKnown.Content.ReadAsStringAsync(), await forUnknown.Content.ReadAsStringAsync());
        // La file part dans l'ordre : une fois l'e-mail du compte connu envoyé, rien n'est parti pour l'inconnu
        await factory.Emails.WaitForAsync(known, m => m.TextBody.Contains("/reset-password?"));
        Assert.False(factory.Emails.AnyTo(unknown));
    }

    [Fact]
    public async Task Le_code_recu_par_e_mail_change_le_mot_de_passe()
    {
        var email = NewEmail();
        await factory.CreateNewUserClientAsync(email);
        var client = Anonymous();
        await ForgotAsync(client, email);

        var reset = await client.PostAsJsonAsync("/api/auth/resetPassword",
            new { email, resetCode = await ResetCodeAsync(factory.Emails, email), newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, email)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Un_lien_de_reinitialisation_deja_utilise_est_signale_des_son_ouverture()
    {
        var email = NewEmail();
        await factory.CreateNewUserClientAsync(email);
        var client = Anonymous();
        await ForgotAsync(client, email);
        var resetCode = await ResetCodeAsync(factory.Emails, email);

        // Ouverture du lien : vérifié sans être consommé, deux fois de suite
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/checkResetCode", new { email, resetCode })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/checkResetCode", new { email, resetCode })).StatusCode);

        await client.PostAsJsonAsync("/api/auth/resetPassword", new { email, resetCode, newPassword = NewPassword });

        // Lien rouvert après usage, adresse inconnue, code altéré : même refus, avec la clé qu'Identity utilise
        foreach (var body in new object[]
                 {
                     new { email, resetCode },
                     new { email = NewEmail(), resetCode },
                     new { email, resetCode = "pas-un-code" },
                 })
        {
            var check = await client.PostAsJsonAsync("/api/auth/checkResetCode", body);
            Assert.Equal(HttpStatusCode.BadRequest, check.StatusCode);
            Assert.Contains("InvalidToken", await check.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Un_code_de_reinitialisation_expire_est_refuse()
    {
        // Hôte dérivé dont les codes de réinitialisation expirent dès leur création
        await using var expiring = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.Zero)));
        var email = NewEmail();
        await expiring.CreateNewUserClientAsync(email);
        var client = expiring.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        await ForgotAsync(client, email);

        var reset = await client.PostAsJsonAsync("/api/auth/resetPassword",
            new { email, resetCode = await ResetCodeAsync(factory.Emails, email), newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, email)).StatusCode);       // mot de passe inchangé
    }

    [Fact]
    public async Task Changer_de_mot_de_passe_exige_l_ancien()
    {
        var email = NewEmail();
        var client = await factory.CreateNewUserClientAsync(email);

        // #19 : /api/me/password remplace POST /api/auth/manage/info (fermé), et garde la session ouverte
        var withoutOld = await client.PostAsJsonAsync("/api/me/password", new { newPassword = NewPassword });
        var wrongOld = await client.PostAsJsonAsync("/api/me/password",
            new { currentPassword = "Mauvais-Password1!", newPassword = NewPassword });
        var rightOld = await client.PostAsJsonAsync("/api/me/password",
            new { currentPassword = SongVaultApiFactory.TestPassword, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, withoutOld.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongOld.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, rightOld.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(Anonymous(), email, NewPassword)).StatusCode);
    }
}