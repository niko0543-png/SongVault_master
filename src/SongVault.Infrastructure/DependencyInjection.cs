using Microsoft.EntityFrameworkCore;
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
            var connectionString = configuration.GetConnectionString("SongVault")
                ?? throw new InvalidOperationException(
                    "Chaîne de connexion 'SongVault' absente. Configurez-la avec dotnet user-secrets.");
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<SongVaultDbContext>());
        services.AddScoped<ISongRepository, SongRepository>();

        services.AddOptions<FileStorageOptions>()
            .Bind(configuration.GetSection(FileStorageOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.RootPath), "FileStorage:RootPath est obligatoire.")
            .ValidateOnStart();                                   // l'API refuse de démarrer si c'est mal configuré
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        return services;
    }
}