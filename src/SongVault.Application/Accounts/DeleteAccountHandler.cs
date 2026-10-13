using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Bands;
using SongVault.Domain.Common;

namespace SongVault.Application.Accounts;

public sealed record DeleteAccountCommand(string Password);

public sealed class DeleteAccountHandler(
    IAccountRepository accounts, IBackgroundJobs jobs, IUnitOfWork unitOfWork, ICurrentUser currentUser,
    TimeProvider clock)
{
    /// <summary>Délai avant l'effacement définitif (ADR 0011).</summary>
    public static readonly TimeSpan PurgeDelay = TimeSpan.FromDays(30);

    /// <summary>
    /// Désactive le compte connecté et programme sa purge. 422 si le mot de passe est faux,
    /// 409 s'il est le dernier Owner d'un groupe qui a d'autres membres.
    /// </summary>
    public async Task HandleAsync(DeleteAccountCommand command, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (!await accounts.CheckPasswordAsync(userId, command.Password, ct))
            throw new DomainException("Mot de passe incorrect.");

        var blocking = new List<string>();
        foreach (var band in await accounts.ListBandsAsync(userId, ct))
        {
            if (band.Memberships.Count == 1) continue;           // seul membre : le groupe part avec le compte, à la purge
            try
            {
                band.RemoveMember(userId);
            }
            catch (LastOwnerException)
            {
                blocking.Add($"« {band.Name} »");
            }
        }

        if (blocking.Count > 0)
            throw new ConflictException(
                $"Vous êtes le dernier Owner de {string.Join(", ", blocking)} : nommez d'abord un autre Owner, ou retirez les autres membres.");

        await accounts.DeactivateAsync(userId, ct);
        jobs.Enqueue(PurgeAccountJob.JobType, userId, clock.GetUtcNow() + PurgeDelay);
        await unitOfWork.SaveChangesAsync(ct);                  // départs, verrouillage et purge : tout ou rien
    }
}