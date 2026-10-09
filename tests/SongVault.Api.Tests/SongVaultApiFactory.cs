using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Infrastructure.Persistence;

using Testcontainers.MsSql;

namespace SongVault.Api.Tests;

public sealed class SongVaultApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Mot de passe conforme à la politique d'Identity (majuscule, minuscule, chiffre, symbole).</summary>
    public const string TestPassword = "Test-Password1!";

    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly string _filesRoot = Path.Combine(Path.GetTempPath(), "songvault-tests", Guid.NewGuid().ToString("N"));

    /// <summary>Cookie de l'utilisateur par défaut, au format "nom=valeur".</summary>
    private string _defaultUserCookie = "";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:SongVault", _sql.GetConnectionString());
        builder.UseSetting("FileStorage:RootPath", _filesRoot);
        builder.UseSetting("RateLimiting:UploadsPerMinute", "1000");
        builder.UseSetting("RateLimiting:AuthPerMinute", "1000");

        // Masque, dans les tests uniquement, les erreurs SQL attendues du test de concurrence
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore.Update", "None");
    }

    private async Task StartAsync()
    {
        // 1. La base
        await _sql.StartAsync();
        using (var scope = Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SongVaultDbContext>().Database.MigrateAsync();

        // 2. L'utilisateur par défaut, inscrit et connecté UNE fois pour toute la suite
        using var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        _defaultUserCookie = await RegisterAndLoginAsync(client, $"default-{Guid.NewGuid():N}@test.local");
    }

    /// <summary>
    /// Client connecté avec l'utilisateur par défaut.
    /// Remplace factory.CreateUserClient() dans les tests qui ne portent pas sur l'authentification.
    /// </summary>
    public HttpClient CreateUserClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", _defaultUserCookie);
        return client;
    }

    /// <summary>Chaîne de connexion vers une autre base du même conteneur (créée par la première migration).</summary>
    public string ConnectionStringFor(string database)
        => new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = database }.ConnectionString;


    /// <summary>Inscrit puis connecte un utilisateur ; renvoie son cookie au format "nom=valeur".</summary>
    public static async Task<string> RegisterAndLoginAsync(HttpClient client, string email)
    {
        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password = TestPassword });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = TestPassword });
        login.EnsureSuccessStatusCode();

        // "songvault.auth=CfDJ8…; path=/; samesite=strict; httponly" → "songvault.auth=CfDJ8…"
        return login.Headers.GetValues("Set-Cookie").First().Split(';', 2)[0];
    }

    // ---- xUnit v2 ----
    public Task InitializeAsync() => StartAsync();
    async Task IAsyncLifetime.DisposeAsync()
    {
        await _sql.DisposeAsync();
        await base.DisposeAsync();
        if (Directory.Exists(_filesRoot)) Directory.Delete(_filesRoot, recursive: true);
    }

    // ---- xUnit v3 : remplacez les deux méthodes ci-dessus par ----
    // public async ValueTask InitializeAsync() => await StartAsync();
    // public override async ValueTask DisposeAsync()
    // { await _sql.DisposeAsync(); await base.DisposeAsync(); if (Directory.Exists(_filesRoot)) Directory.Delete(_filesRoot, true); }


}