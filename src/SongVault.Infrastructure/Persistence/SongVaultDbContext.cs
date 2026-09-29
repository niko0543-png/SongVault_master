using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence;

public sealed class SongVaultDbContext(DbContextOptions<SongVaultDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Song> Songs => Set<Song>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(SongVaultDbContext).Assembly);

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);           // RowVersion différente
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConcurrencyConflictException(ex);           // violation d'index unique (filet de sécurité)
        }
    }

    public void DiscardChanges() => ChangeTracker.Clear();
}