using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Accounts;
using SongVault.Domain.Bands;
using SongVault.Domain.Files;
using SongVault.Domain.Invitations;
using SongVault.Domain.Songs;
using SongVault.Infrastructure.Jobs;
using SongVault.Domain.Comments;
using SongVault.Domain.Releases;
using SongVault.Domain.Activity;
using SongVault.Domain.Notifications;
using SongVault.Domain.Sharing;
using SongVault.Domain.Billing;
using SongVault.Domain.Audit;

namespace SongVault.Infrastructure.Persistence;

public sealed class SongVaultDbContext(
    DbContextOptions<SongVaultDbContext> options, IBandContext? bandContext = null)
    : IdentityDbContext<IdentityUser>(options), IUnitOfWork, IDataProtectionKeyContext
{
    /// <summary>Nom du filtre qui limite Songs et Releases au groupe actif (IgnoreQueryFilters([BandFilter]) pour l'ôter).</summary>
    public const string BandFilter = "Band";
    /// <summary>
    /// Nom du filtre qui cache la corbeille (#20, ADR 0027) : morceaux, versions et fichiers dont DeletedAt est renseigné.
    /// IgnoreQueryFilters([SoftDeleteFilter]) les montre, le filtre du groupe restant actif.
    /// </summary>
    public const string SoftDeleteFilter = "SoftDelete";
    public DbSet<Song> Songs => Set<Song>();
    public DbSet<Band> Bands => Set<Band>();
    public DbSet<BandMembership> BandMemberships => Set<BandMembership>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    /// <summary>Tâches de fond (ADR 0011).</summary>
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<DataExport> DataExports => Set<DataExport>();
    /// <summary>Envois directs annoncés, pas encore confirmés (ADR 0012).</summary>
    public DbSet<FileUpload> FileUploads => Set<FileUpload>();
    /// <summary>Commentaires horodatés sur les fichiers audio (ADR 0014).</summary>
    public DbSet<Comment> Comments => Set<Comment>();
    /// <summary>Fil d'activité et badges (ADR 0017).</summary>
    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();
    public DbSet<SongView> SongViews => Set<SongView>();
    /// <summary>Notifications par e-mail (ADR 0018).</summary>
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationDigestItem> NotificationDigestItems => Set<NotificationDigestItem>();
    /// <summary>Sorties (single, EP, album) et leurs pistes (ADR 0016).</summary>
    public DbSet<Release> Releases => Set<Release>();
    /// <summary>Liens de partage externes et leur journal (ADR 0019).</summary>
    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();
    public DbSet<ShareLinkAccess> ShareLinkAccesses => Set<ShareLinkAccess>();
    /// <summary>Forme d'onde et MP3 d'écoute des fichiers audio (ADR 0021).</summary>
    public DbSet<AudioPreview> AudioPreviews => Set<AudioPreview>();
    /// <summary>Analyse antivirus des fichiers (ADR 0024).</summary>
    public DbSet<FileScan> FileScans => Set<FileScan>();
    /// <summary>Formules, abonnements des groupes et événements Stripe déjà traités (ADR 0023).</summary>
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<ProcessedStripeEvent> ProcessedStripeEvents => Set<ProcessedStripeEvent>();
    /// <summary>Journal d'audit des groupes et historique de connexion des comptes (ADR 0026).</summary>
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<AccountEvent> AccountEvents => Set<AccountEvent>();
    /// <summary>Clés qui signent cookies et liens (ADR 0010) : en base pour survivre à une reconstruction de l'API.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // Relu par EF à chaque requête. null hors requête HTTP : le filtre ne laisse alors passer aucun morceau.
    private Guid? CurrentBandId => bandContext?.BandId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);      // tables AspNetUsers, AspNetRoles… : OBLIGATOIRE, et en premier
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SongVaultDbContext).Assembly);

        // Défense en profondeur : même si une future requête oublie BandSongs, elle ne sort pas du groupe actif.
        modelBuilder.Entity<Song>().HasQueryFilter(BandFilter, s => s.BandId == CurrentBandId);
        modelBuilder.Entity<Release>().HasQueryFilter(BandFilter, r => r.BandId == CurrentBandId);
        // #20 : la corbeille n'apparaît nulle part, sauf où on la demande
        modelBuilder.Entity<Song>().HasQueryFilter(SoftDeleteFilter, s => s.DeletedAt == null);
        modelBuilder.Entity<SongVersion>().HasQueryFilter(SoftDeleteFilter, v => v.DeletedAt == null);
        modelBuilder.Entity<SongFile>().HasQueryFilter(SoftDeleteFilter, f => f.DeletedAt == null);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // #19 : le journal d'audit ne se modifie pas depuis l'application (le déclencheur SQL le refuse aussi)
        if (ChangeTracker.Entries<AuditEntry>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Journal d'audit : une ligne ne se modifie ni ne se supprime.");
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConcurrencyConflictException(ex);
        }
    }

    public void DiscardChanges() => ChangeTracker.Clear();

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        await using var transaction = await Database.BeginTransactionAsync(ct);
        await work(ct);
        await transaction.CommitAsync(ct);     // exception avant ce point : la transaction est annulée à sa libération
    }
}