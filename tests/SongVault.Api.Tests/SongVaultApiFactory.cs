using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SongVault.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace SongVault.Api.Tests;

public sealed class SongVaultApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");   // pas de user-secrets, pas d'OpenAPI
        builder.UseSetting("ConnectionStrings:SongVault", _sql.GetConnectionString());
    }

    private async Task StartAsync()
    {
        await _sql.StartAsync();
        using var scope = Services.CreateScope();          // démarre l'API (ConfigureWebHost est appelé ici)
        var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
        await db.Database.MigrateAsync();                   // mêmes migrations qu'en réel
    }

    // ---- xUnit v2 ----
    public Task InitializeAsync() => StartAsync();
    async Task IAsyncLifetime.DisposeAsync()
    {
        await _sql.DisposeAsync();
        await base.DisposeAsync();
    }

    // ---- xUnit v3 : remplacez les deux méthodes ci-dessus par ----
    // public async ValueTask InitializeAsync() => await StartAsync();
    // public override async ValueTask DisposeAsync() { await _sql.DisposeAsync(); await base.DisposeAsync(); }
}