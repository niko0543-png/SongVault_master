using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence;

public sealed class SongVaultDbContext(DbContextOptions<SongVaultDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Song> Songs => Set<Song>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(SongVaultDbContext).Assembly);
}