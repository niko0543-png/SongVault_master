using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using SongVault.Application.Abstractions;
using SongVault.Infrastructure.Persistence;
using Testcontainers.MsSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SongVault.Infrastructure.Jobs;
using SongVault.Application.Notifications;

namespace SongVault.Api.Tests;

public sealed class SongVaultApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Mot de passe conforme à la politique d'Identity (majuscule, minuscule, chiffre, symbole).</summary>
    public const string TestPassword = "Test-Password1!";

    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly string _filesRoot = Path.Combine(Path.GetTempPath(), "songvault-tests", Guid.NewGuid().ToString("N"));

    /// <summary>Cookie de l'utilisateur par défaut, au format "nom=valeur".</summary>
    private string _defaultUserCookie = "";

    /// <summary>E-mails « envoyés » pendant les tests (remplace LogEmailSender).</summary>
    public CapturingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:SongVault", _sql.GetConnectionString());
        builder.UseSetting("FileStorage:RootPath", _filesRoot);
        builder.UseSetting("RateLimiting:UploadsPerMinute", "1000");
        builder.UseSetting("RateLimiting:AuthPerMinute", "1000");
        builder.UseSetting("RateLimiting:SharePerMinute", "1000");
        builder.UseSetting("App:PublicUrl", "http://localhost:8080");
        // #5 : pas de service d'arrière-plan ; les tests lancent les tâches avec RunDueJobsAsync
        builder.UseSetting("BackgroundJobs:Enabled", "false");
        // #12 : pas de préparation automatique des notifications ; les tests appellent RunNotificationsAsync
        builder.UseSetting("Notifications:Enabled", "false");
        // #16 : formules et quotas actifs, paiement fermé (Billing:Provider None) ; le webhook vérifie ce secret
        builder.UseSetting("Billing:Stripe:WebhookSecret", SongVault.Api.Tests.Billing.StripeTestHelpers.WebhookSecret);
        builder.ConfigureTestServices(services => services.AddSingleton<IEmailSender>(Emails));
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
        _defaultUserCookie = await RegisterAndLoginAsync(Services, client, $"default-{Guid.NewGuid():N}@test.local");
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


    /// <summary>Inscrit, confirme l'adresse puis connecte un utilisateur ; renvoie son cookie au format "nom=valeur".</summary>
    public static async Task<string> RegisterAndLoginAsync(IServiceProvider services, HttpClient client, string email)
    {
        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password = TestPassword });
        register.EnsureSuccessStatusCode();
        await ConfirmEmailAsync(services, email);           // #4 : connexion refusée sans adresse confirmée

        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = TestPassword });
        login.EnsureSuccessStatusCode();

        // "songvault.auth=CfDJ8…; path=/; samesite=strict; httponly" → "songvault.auth=CfDJ8…"
        return login.Headers.GetValues("Set-Cookie").First().Split(';', 2)[0];
    }

    /// <summary>Marque l'adresse comme confirmée, directement en base. Le parcours par e-mail est testé dans AccountTests.</summary>
    public static async Task ConfirmEmailAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await users.FindByEmailAsync(email) ?? throw new InvalidOperationException($"Compte {email} introuvable.");
        user.EmailConfirmed = true;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Code)));
    }

    /// <summary>Exécute toutes les tâches dues à l'heure de l'hôte, une par une, jusqu'à ce qu'il n'en reste plus.</summary>
    public static async Task RunDueJobsAsync(IServiceProvider services)
    {
        var runner = services.GetRequiredService<BackgroundJobRunner>();
        while (await runner.RunNextAsync(CancellationToken.None)) { }
    }

    /// <summary>
    /// Prépare les notifications de tous les nouveaux événements (#12), puis exécute les tâches dues :
    /// ce que ferait NotificationWorker, suivi de BackgroundJobWorker.
    /// </summary>
    public static async Task RunNotificationsAsync(IServiceProvider services)
    {
        using (var scope = services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
            while (await dispatcher.RunOnceAsync(CancellationToken.None) > 0) { }
        }
        await RunDueJobsAsync(services);
    }


    /// <summary>Hôte dérivé avec cette horloge (et d'éventuels services en plus). À libérer avec await using.</summary>
    public WebApplicationFactory<Program> WithClock(TimeProvider clock, Action<IServiceCollection>? services = null)
        => WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton(clock);
            services?.Invoke(s);
        }));

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