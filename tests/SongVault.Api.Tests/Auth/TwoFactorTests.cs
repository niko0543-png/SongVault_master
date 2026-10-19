using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

using SongVault.Api.Contracts.Me;
using SongVault.Domain.Accounts;

namespace SongVault.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public sealed class TwoFactorTests(SongVaultApiFactory factory)
{
    private HttpClient Anonymous() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string? code = null,
        string? recoveryCode = null, string password = SongVaultApiFactory.TestPassword)
        => client.PostAsJsonAsync("/api/auth/login?useCookies=true",
            new { email, password, twoFactorCode = code, twoFactorRecoveryCode = recoveryCode });

    /// <summary>Remplace le cookie du client par celui que la réponse vient de renouveler (RefreshSignInAsync).</summary>
    private static HttpResponseMessage KeepCookie(HttpClient client, HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.FirstOrDefault(c => c.StartsWith("songvault.auth=", StringComparison.Ordinal)) is { } cookie)
        {
            client.DefaultRequestHeaders.Remove("Cookie");
            client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';', 2)[0]);
        }
        return response;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object? body = null)
        => KeepCookie(client, body is null ? await client.PostAsync(url, null) : await client.PostAsJsonAsync(url, body));

    /// <summary>Nouveau compte, double authentification activée : son client, son adresse, sa clé et ses codes de secours.</summary>
    private async Task<(HttpClient Client, string Email, string Key, IReadOnlyList<string> Codes)> EnabledAsync()
    {
        var email = $"2fa-{Guid.NewGuid():N}@test.local";
        var client = await factory.CreateNewUserClientAsync(email);

        var setup = await PostAsync(client, "/api/me/two-factor/setup");
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        var key = (await setup.Content.ReadFromJsonAsync<TwoFactorSetupResponse>(TestJson.Options))!;
        Assert.StartsWith("otpauth://totp/SongVault:", key.AuthenticatorUri);
        Assert.Contains($"secret={key.SharedKey.Replace(" ", "")}", key.AuthenticatorUri);

        var enable = await PostAsync(client, "/api/me/two-factor/enable", new { code = Totp.Code(key.SharedKey) });
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        var codes = (await enable.Content.ReadFromJsonAsync<RecoveryCodesResponse>(TestJson.Options))!.RecoveryCodes;
        return (client, email, key.SharedKey, codes);
    }

    private static async Task<List<AccountEventType>> EventsAsync(HttpClient client)
        => [.. (await client.GetFromJsonAsync<List<AccountEventResponse>>("/api/me/account-events", TestJson.Options))!
            .Select(e => e.Type)];

    [Fact]
    public async Task Activee_la_connexion_demande_le_code_puis_l_accepte()
    {
        var (client, email, key, codes) = await EnabledAsync();
        Assert.Equal(10, codes.Count);
        var status = await client.GetFromJsonAsync<TwoFactorStatusResponse>("/api/me/two-factor", TestJson.Options);
        Assert.Equal((true, 10), (status!.Enabled, status.RecoveryCodesLeft));

        var withoutCode = await LoginAsync(Anonymous(), email);
        Assert.Equal(HttpStatusCode.Unauthorized, withoutCode.StatusCode);
        Assert.Equal("RequiresTwoFactor", (await withoutCode.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(Anonymous(), email, code: Totp.Code(key))).StatusCode);
        Assert.Contains(AccountEventType.SignedInWithCode, await EventsAsync(client));
    }

    [Fact]
    public async Task Un_code_de_secours_ne_sert_qu_une_fois()
    {
        var (client, email, _, codes) = await EnabledAsync();

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(Anonymous(), email, recoveryCode: codes[0])).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(Anonymous(), email, recoveryCode: codes[0])).StatusCode);

        var status = await client.GetFromJsonAsync<TwoFactorStatusResponse>("/api/me/two-factor", TestJson.Options);
        Assert.Equal(9, status!.RecoveryCodesLeft);
        var events = await EventsAsync(client);
        Assert.Contains(AccountEventType.SignedInWithRecoveryCode, events);
        Assert.Contains(AccountEventType.CodeRejected, events);
    }

    [Fact]
    public async Task Un_mauvais_code_a_l_activation_ne_change_rien()
    {
        var client = await factory.CreateNewUserClientAsync();
        await PostAsync(client, "/api/me/two-factor/setup");

        var enable = await PostAsync(client, "/api/me/two-factor/enable", new { code = "12345x" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, enable.StatusCode);
        Assert.False((await client.GetFromJsonAsync<TwoFactorStatusResponse>("/api/me/two-factor", TestJson.Options))!.Enabled);
    }

    [Fact]
    public async Task Desactiver_redemande_le_mot_de_passe()
    {
        var (client, email, _, _) = await EnabledAsync();

        var wrong = await PostAsync(client, "/api/me/two-factor/disable", new { password = "Mauvais-Passe1!" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrong.StatusCode);

        var disable = await PostAsync(client, "/api/me/two-factor/disable", new { password = SongVaultApiFactory.TestPassword });
        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(Anonymous(), email)).StatusCode);   // plus de code demandé
        Assert.Contains(AccountEventType.TwoFactorDisabled, await EventsAsync(client));
    }

    [Fact]
    public async Task Renouveler_les_codes_de_secours_invalide_les_anciens()
    {
        var (client, email, _, codes) = await EnabledAsync();

        var renew = await PostAsync(client, "/api/me/two-factor/recovery-codes", new { password = SongVaultApiFactory.TestPassword });
        var fresh = (await renew.Content.ReadFromJsonAsync<RecoveryCodesResponse>(TestJson.Options))!.RecoveryCodes;

        Assert.Equal(10, fresh.Count);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(Anonymous(), email, recoveryCode: codes[1])).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(Anonymous(), email, recoveryCode: fresh[0])).StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/auth/manage/2fa")]
    [InlineData("POST", "/api/auth/manage/info")]
    public async Task Les_routes_d_Identity_qui_contournent_le_mot_de_passe_sont_fermees(string method, string url)
    {
        var client = await factory.CreateNewUserClientAsync();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = JsonContent.Create(new { enable = false, oldPassword = "x", newPassword = "Autre-Passe1!" }),
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/manage/info")).StatusCode);   // la lecture reste
    }

    [Fact]
    public async Task Changer_le_mot_de_passe_garde_cette_session_et_l_ecrit()
    {
        var email = $"pwd-{Guid.NewGuid():N}@test.local";
        var client = await factory.CreateNewUserClientAsync(email);

        var wrong = await PostAsync(client, "/api/me/password",
            new { currentPassword = "Mauvais-Passe1!", newPassword = "Nouveau-Passe2!" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("PasswordMismatch",
            (await wrong.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Keys);

        var change = await PostAsync(client, "/api/me/password",
            new { currentPassword = SongVaultApiFactory.TestPassword, newPassword = "Nouveau-Passe2!" });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(Anonymous(), email, password: "Nouveau-Passe2!")).StatusCode);
        Assert.Contains(AccountEventType.PasswordChanged, await EventsAsync(client));
    }

    [Fact]
    public async Task L_historique_note_un_mot_de_passe_faux_et_une_connexion()
    {
        var email = $"hist-{Guid.NewGuid():N}@test.local";
        var client = await factory.CreateNewUserClientAsync(email);

        await LoginAsync(Anonymous(), email, password: "Mauvais-Passe1!");

        var events = await EventsAsync(client);
        Assert.Equal([AccountEventType.SignInFailed, AccountEventType.SignedIn], events);   // le plus récent d'abord
        // Adresse inconnue : refusée normalement, et rien à écrire (pas de compte à qui le montrer)
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await LoginAsync(Anonymous(), $"inconnu-{Guid.NewGuid():N}@test.local")).StatusCode);
    }
}