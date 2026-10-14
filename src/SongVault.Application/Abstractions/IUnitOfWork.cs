namespace SongVault.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Oublie toutes les entités suivies (avant une nouvelle tentative).</summary>
    void DiscardChanges();


    /// <summary>
    /// Exécute work dans une transaction : tous ses SaveChangesAsync sont validés ensemble, ou aucun
    /// si work lève une exception (#10, pistes d'une sortie en deux passes).
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct);
}