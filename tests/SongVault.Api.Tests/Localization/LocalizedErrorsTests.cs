using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;

using SongVault.Api.Contracts.Auth;

namespace SongVault.Api.Tests.Localization;

/// <summary>#26 : messages d'erreur selon Accept-Language, et langue enregistrée sur le compte.</summary>
[Collection(ApiCollection.Name)]
public sealed class LocalizedErrorsTests(SongVaultApiFactory factory)
{
    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options));

    private static HttpRequestMessage Get(string url, string? language)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (language is not null) request.Headers.AcceptLanguage.ParseAdd(language);
        return request;
    }

    [Fact]
    public async Task Une_erreur_en_anglais_garde_le_meme_code_qu_en_francais()
    {
        var client = await factory.CreateNewUserClientAsync();
        var missing = Guid.NewGuid();
        var url = $"{await client.SongsUrlAsync()}/{missing}";

        var english = await client.SendAsync(Get(url, "en-US,en;q=0.9"));
        var french = await client.SendAsync(Get(url, null));

        Assert.Equal(HttpStatusCode.NotFound, english.StatusCode);
        var en = await ProblemAsync(english);
        Assert.Equal("Resource not found", en.GetProperty("title").GetString());
        Assert.Equal($"Song '{missing}' not found.", en.GetProperty("detail").GetString());
        Assert.Equal("common.notFound", en.GetProperty("code").GetString());
        Assert.Equal("en", english.Content.Headers.ContentLanguage.Single());

        var fr = await ProblemAsync(french);
        Assert.Equal("Ressource introuvable", fr.GetProperty("title").GetString());
        Assert.Equal($"Song '{missing}' introuvable.", fr.GetProperty("detail").GetString());
        Assert.Equal("common.notFound", fr.GetProperty("code").GetString());
    }

    [Fact]
    public async Task La_langue_choisie_est_gardee_sur_le_compte()
    {
        var client = await factory.CreateNewUserClientAsync();
        Assert.Null((await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", TestJson.Options))!.Locale);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/me/locale", new { locale = "en" })).StatusCode);
        Assert.Equal("en", (await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", TestJson.Options))!.Locale);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/me/locale", new { locale = "fr" })).StatusCode);
        Assert.Equal("fr", (await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", TestJson.Options))!.Locale);
    }

    [Fact]
    public async Task Une_langue_non_prise_en_charge_est_refusee_avec_un_message_traduit()
    {
        var client = await factory.CreateNewUserClientAsync();
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/me/locale") { Content = JsonContent.Create(new { locale = "de" }) };
        request.Headers.AcceptLanguage.ParseAdd("en");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal("Unsupported language: choose fr or en.", problem.GetProperty("errors").GetProperty("Locale")[0].GetString());
    }

    [Fact]
    public async Task Anonyme_ne_peut_pas_enregistrer_de_langue()
    {
        var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

        var response = await anonymous.PutAsJsonAsync("/api/me/locale", new { locale = "en" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
