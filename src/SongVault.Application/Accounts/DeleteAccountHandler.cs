using SongVault.Application.Abstractions;
using SongVault.Application.Audit;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Audit;
using SongVault.Domain.Bands;
using SongVault.Domain.Common;

namespace SongVault.Application.Accounts;

public sealed record DeleteAccountCommand(string Password);

public sealed class DeleteAccountHandler(
    IAccountRepository accounts, IBackgroundJobs jobs, IUnitOfWork unitOfWork, ICurrentUser currentUser,
    TimeProvider clock, IBillingRepository billing, AuditLog audit)
{
    /// <summary>Délai avant l'effacement définitif (ADR 0011).</summary>
    public static readonly TimeSpan PurgeDelay = TimeSpan.FromDays(30);

    /// <summary>
    /// Désactive le compte connecté et programme sa purge. 422 si le mot de passe est faux,
    /// 409 s'il est le dernier Owner d'un groupe qui a d'autres membres, ou si un groupe dont il est le seul
    /// membre a un abonnement Stripe qui n'est pas résilié (#16) : il serait encore prélevé après la suppression.
    /// </summary>
    public async Task HandleAsync(DeleteAccountCommand command, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (!await accounts.CheckPasswordAsync(userId, command.Password, ct))
            throw new DomainException("Mot de passe incorrect.");

        var bands = await accounts.ListBandsAsync(userId, ct);
        var solo = bands.Where(b => b.Memberships.Count == 1).ToList();
        var paying = (await billing.ListSubscriptionsAsync([.. solo.Select(b => b.Id)], ct))
            .Where(s => s.IsLiveOnStripe && !s.CancelAtPeriodEnd)
            .Select(s => $"« {solo.Single(b => b.Id == s.BandId).Name} »")
            .ToList();
        if (paying.Count > 0)
            throw new ConflictException(
                $"{string.Join(", ", paying)} {(paying.Count > 1 ? "ont" : "a")} un abonnement en cours : résiliez-le depuis la page Facturation, puis supprimez votre compte.");

        var now = clock.GetUtcNow();
        var blocking = new List<string>();
        foreach (var band in bands)
        {
            if (band.Memberships.Count == 1) continue;           // seul membre : le groupe part avec le compte, à la purge
            try
            {
                band.RemoveMember(userId);
                audit.Record(band.Id, AuditAction.MemberLeft, null, now, details: "suppression du compte");   // #19
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
        jobs.Enqueue(PurgeAccountJob.JobType, userId, now + PurgeDelay);
        await unitOfWork.SaveChangesAsync(ct);                  // départs, verrouillage et purge : tout ou rien
    }
}