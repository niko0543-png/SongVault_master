using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;

using SongVault.Api.Contracts.Auth;

namespace SongVault.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public sealed class AuthTests(SongVaultApiFactory factory)
{
    /// <summary>Client SANS cookie et qui ne suit PAS les redirections : on voit la réponse brute.</summary>
    private HttpClient Anonymous() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    [Theory]
    [InlineData("/api/bands")]
    [InlineData("/api/files/policy")]
    [InlineData("/api/auth/me")]
    public async Task Anonyme_recoit_401_et_pas_une_redirection(string url)
    {
        var response = await Anonymous().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);    // et surtout pas 302
    }

    [Fact]
    public async Task Les_health_checks_restent_anonymes()
        => Assert.Equal(HttpStatusCode.OK, (await Anonymous().GetAsync("/health/live")).StatusCode);

    [Fact]
    public async Task Inscription_puis_connexion_donne_acces_a_l_api()
    {
        var client = await factory.CreateNewUserClientAsync();

        var songs = await client.SongsUrlAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(songs)).StatusCode);
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.EndsWith("@test.local", me!.Email);
    }

    [Fact]
    public async Task Le_cookie_est_HttpOnly_et_SameSite_Strict()
    {
        var client = Anonymous();
        var email = $"cookie-{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = SongVaultApiFactory.TestPassword });

        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=true",
            new { email, password = SongVaultApiFactory.TestPassword });

        var setCookie = login.Headers.GetValues("Set-Cookie").First().ToLowerInvariant();
        Assert.Contains("httponly", setCookie);
        Assert.Contains("samesite=strict", setCookie);
    }

    [Fact]
    public async Task Mauvais_mot_de_passe_renvoie_401()
    {
        var response = await Anonymous().PostAsJsonAsync("/api/auth/login?useCookies=true",
            new { email = "inconnu@test.local", password = "Faux-mot-de-passe1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Apres_deconnexion_le_cookie_ne_donne_plus_acces()
    {
        // Client qui GÈRE les cookies : il reçoit le cookie vide renvoyé par /logout, comme un navigateur
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var email = $"logout-{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = SongVaultApiFactory.TestPassword });
        await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = SongVaultApiFactory.TestPassword });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
}