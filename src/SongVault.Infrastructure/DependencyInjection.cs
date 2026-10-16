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
using SongVault.Application.Notifications;
using SongVault.Infrastructure.Notifications;
using SongVault.Infrastructure.Sharing;
using SongVault.Infrastructure.Audio;
using SongVault.Infrastructure.Billing;

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
        services.AddScoped<IShareLinkRepository, ShareLinkRepository>();
        services.AddScoped<IAudioPreviewRepository, AudioPreviewRepository>();
        services.AddScoped<IBillingRepository, BillingRepository>();

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

        // #12 : notifications par e-mail, préparées en arrière-plan (ADR 0018)
        services.AddOptions<NotificationOptions>()
            .Bind(configuration.GetSection(NotificationOptions.SectionName))
            .Validate(o => o.DispatchInterval > TimeSpan.Zero, "Notifications:DispatchInterval doit être positif.")
            .Validate(o => o.BatchSize >= 1, "Notifications:BatchSize doit valoir au moins 1.")
            .Validate(o => o.DigestHour is >= 0 and <= 23, "Notifications:DigestHour va de 0 à 23.")
            .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.DigestTimeZone, out _),
                "Notifications:DigestTimeZone doit être un fuseau connu (ex. Europe/Paris).")
            .ValidateOnStart();
        services.AddSingleton<IUnsubscribeTokens, UnsubscribeTokens>();
        services.AddHostedService<NotificationWorker>();

        // #15 : forme d'onde et MP3 d'écoute, calculés par ffmpeg en tâche de fond (ADR 0021)
        services.AddOptions<AudioPreviewOptions>()
            .Bind(configuration.GetSection(AudioPreviewOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.FfmpegPath), "AudioPreview:FfmpegPath est obligatoire.")
            .Validate(o => o.Timeout > TimeSpan.Zero, "AudioPreview:Timeout doit être positif.")
            .Validate(o => o.Mp3BitrateKbps is >= 32 and <= 320, "AudioPreview:Mp3BitrateKbps va de 32 à 320.")
            .ValidateOnStart();
        services.AddSingleton<IAudioProcessor, FfmpegAudioProcessor>();
        // #16 : formules, quotas et paiement par Stripe (ADR 0023)
        services.AddOptions<BillingOptions>()
            .Bind(configuration.GetSection(BillingOptions.SectionName))
            .Validate(o => o.Provider is BillingOptions.NoneProvider or BillingOptions.StripeProvider,
                "Billing:Provider doit valoir None ou Stripe.")
            .Validate(o => o.Provider != BillingOptions.StripeProvider || o.Stripe.SecretKey.StartsWith("sk_", StringComparison.Ordinal),
                "Billing:Stripe:SecretKey (sk_test_… ou sk_live_…) est obligatoire avec Stripe.")
            .Validate(o => o.Provider != BillingOptions.StripeProvider || o.Stripe.WebhookSecret.StartsWith("whsec_", StringComparison.Ordinal),
                "Billing:Stripe:WebhookSecret (whsec_…) est obligatoire avec Stripe.")
            .Validate(o => o.Provider != BillingOptions.StripeProvider || o.Stripe.Prices.Count > 0,
                "Billing:Stripe:Prices doit donner le prix Stripe de chaque formule payante.")
            .ValidateOnStart();
        if (configuration[$"{BillingOptions.SectionName}:Provider"] == BillingOptions.StripeProvider)
            services.AddHttpClient<IPaymentGateway, StripePaymentGateway>(c =>
            {
                c.BaseAddress = new Uri("https://api.stripe.com/");
                c.Timeout = TimeSpan.FromSeconds(30);
            });
        else
            services.AddSingleton<IPaymentGateway, DisabledPaymentGateway>();
        services.AddSingleton<IPaymentEventReader, StripeEventReader>();
        services.AddHostedService<StorageRecountWorker>();

        // #13 : liens de partage externes (ADR 0019)
        services.AddSingleton<ISharePasswordHasher, SharePasswordHasher>();
        services.AddSingleton<IShareUnlockTokens, ShareUnlockTokens>();

        return services;
    }
}