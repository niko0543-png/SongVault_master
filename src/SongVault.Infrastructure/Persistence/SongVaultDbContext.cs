using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Bands;
using SongVault.Domain.Songs;
using SongVault.Domain.Invitations;

namespace SongVault.Infrastructure.Persistence;

public sealed class SongVaultDbContext(
    DbContextOptions<SongVaultDbContext> options, IBandContext? bandContext = null)
    : IdentityDbContext<IdentityUser>(options), IUnitOfWork
{
    /// <summary>Nom du filtre qui limite Songs au groupe actif (IgnoreQueryFilters([BandFilter]) pour l'ôter).</summary>
    public const string BandFilter = "Band";

    public DbSet<Song> Songs => Set<Song>();
    public DbSet<Band> Bands => Set<Band>();
    public DbSet<BandMembership> BandMemberships => Set<BandMembership>();
    public DbSet<Invitation> Invitations => Set<Invitation>();

    // Relu par EF à chaque requête. null hors requête HTTP : le filtre ne laisse alors passer aucun morceau.
    private Guid? CurrentBandId => bandContext?.BandId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);      // tables AspNetUsers, AspNetRoles… : OBLIGATOIRE, et en premier
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SongVaultDbContext).Assembly);

        // Défense en profondeur : même si une future requête oublie BandSongs, elle ne sort pas du groupe actif.
        modelBuilder.Entity<Song>().HasQueryFilter(BandFilter, s => s.BandId == CurrentBandId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
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
}