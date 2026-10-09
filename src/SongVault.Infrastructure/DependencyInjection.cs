using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Application.Abstractions;
using SongVault.Infrastructure.Persistence;
using SongVault.Infrastructure.Persistence.Repositories;
using SongVault.Infrastructure.Storage;

namespace SongVault.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SongVaultDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("SongVault");

            // Absente pendant la conception (dotnet ef, bundle construit dans Docker) :
            // on configure SQL Server SANS chaîne ; elle sera fournie plus tard (--connection).
            if (string.IsNullOrWhiteSpace(connectionString))
                options.UseSqlServer();
            else
                options.UseSqlServer(connectionString);

            // Song a un filtre de groupe, SongVersion non : avertissement attendu, voir SongVaultDbContext
            options.ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<SongVaultDbContext>());
        services.AddScoped<ISongRepository, SongRepository>();
        services.AddScoped<IBandRepository, BandRepository>();

        services.AddOptions<FileStorageOptions>()
            .Bind(configuration.GetSection(FileStorageOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.RootPath), "FileStorage:RootPath est obligatoire.")
            .ValidateOnStart();                                   // l'API refuse de démarrer si c'est mal configuré
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        return services;
    }
}