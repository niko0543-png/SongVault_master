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

    private readonly string _filesRoot = Path.Combine(Path.GetTempPath(), "songvault-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:SongVault", _sql.GetConnectionString());
        builder.UseSetting("FileStorage:RootPath", _filesRoot);

        // NOUVEAU (31.9) : la suite de tests ne doit JAMAIS buter sur la limite de débit
        builder.UseSetting("RateLimiting:UploadsPerMinute", "1000");
        builder.UseSetting("RateLimiting:AuthPerMinute", "1000");
    }

    private async Task StartAsync()
    {
        await _sql.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SongVaultDbContext>().Database.MigrateAsync();
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