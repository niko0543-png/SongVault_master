namespace SongVault.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Oublie toutes les entités suivies (avant une nouvelle tentative).</summary>
    void DiscardChanges();
}