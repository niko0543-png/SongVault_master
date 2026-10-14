using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SongVault.Infrastructure.Jobs;
using SongVault.Application.Abstractions;
using SongVault.Infrastructure.Email;
using SongVault.Infrastructure.Persistence;
using SongVault.Infrastructure.Persistence.Repositories;
using SongVault.Infrastructure.Storage;
using Microsoft.Extensions.Options;

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
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IFileUploadRepository, FileUploadRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<ISongViewRepository, SongViewRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IReleaseRepository, ReleaseRepository>();

        services.AddOptions<FileStorageOptions>()
         .Bind(configuration.GetSection(FileStorageOptions.SectionName))
         .Validate(o => o.Provider is FileStorageOptions.LocalProvider or FileStorageOptions.AzureBlobProvider,
             "FileStorage:Provider doit valoir Local ou AzureBlob.")
         .Validate(o => o.Provider != FileStorageOptions.LocalProvider || !string.IsNullOrWhiteSpace(o.RootPath),
             "FileStorage:RootPath est obligatoire avec le stockage Local.")
         .Validate(o => o.Provider != FileStorageOptions.AzureBlobProvider
                        || !string.IsNullOrWhiteSpace(o.AzureBlob.ConnectionString),
             "FileStorage:AzureBlob:ConnectionString est obligatoire avec le stockage AzureBlob.")
         .ValidateOnStart();                                   // l'API refuse de démarrer si c'est mal configuré

        // #6 : Local ou AzureBlob, choisi par FileStorage:Provider (ADR 0012). Seul le stockage choisi est construit.
        services.AddSingleton<LocalFileStorageService>();
        services.AddSingleton<AzureBlobFileStorageService>();
        services.AddSingleton<IFileStorageService>(sp =>
            sp.GetRequiredService<IOptions<FileStorageOptions>>().Value.Provider == FileStorageOptions.AzureBlobProvider
                ? sp.GetRequiredService<AzureBlobFileStorageService>()
                : sp.GetRequiredService<LocalFileStorageService>());
        services.AddHostedService<AzureBlobStorageInitializer>();
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(o => o.Provider is EmailOptions.LogProvider or EmailOptions.BrevoProvider,
                "Email:Provider doit valoir Log ou Brevo.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.FromAddress), "Email:FromAddress est obligatoire.")
            .Validate(o => o.Provider != EmailOptions.BrevoProvider || !string.IsNullOrWhiteSpace(o.Brevo.ApiKey),
                "Email:Brevo:ApiKey est obligatoire avec le fournisseur Brevo.")
            .ValidateOnStart();

        // Le fournisseur se choisit au démarrage : changer Email:Provider demande un redémarrage de l'API
        if (configuration[$"{EmailOptions.SectionName}:Provider"] == EmailOptions.BrevoProvider)
            services.AddHttpClient<IEmailSender, BrevoEmailSender>(c => c.BaseAddress = new Uri("https://api.brevo.com/"));
        else
            services.AddSingleton<IEmailSender, LogEmailSender>();

        // #4 : file d'envoi en mémoire, vidée en arrière-plan (e-mails d'Identity, ADR 0010)
        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
        services.AddHostedService<EmailQueueWorker>();

        // #5 : tâches de fond en base (ADR 0011)
        services.AddOptions<BackgroundJobOptions>()
            .Bind(configuration.GetSection(BackgroundJobOptions.SectionName))
            .Validate(o => o.MaxAttempts >= 1, "BackgroundJobs:MaxAttempts doit valoir au moins 1.")
            .Validate(o => o.PollInterval > TimeSpan.Zero, "BackgroundJobs:PollInterval doit être positif.")
            .ValidateOnStart();
        services.AddScoped<IBackgroundJobs, BackgroundJobScheduler>();
        services.AddSingleton<BackgroundJobRunner>();
        services.AddHostedService<BackgroundJobWorker>();

        return services;
    }
}