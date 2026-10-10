using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Application.Bands;
using SongVault.Application.Common;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Bands;
using SongVault.Domain.Common;
using SongVault.Domain.Invitations;

namespace SongVault.Application.Invitations;

public sealed record CreateInvitationCommand(string Email, BandRole Role);

public sealed class CreateInvitationHandler(
    IInvitationRepository invitations, IBandRepository bands, IUnitOfWork unitOfWork, IBandContext bandContext,
    ICurrentUser currentUser, IEmailSender emailSender, IOptions<AppOptions> app, TimeProvider clock)
{
    /// <summary>
    /// Invite une adresse dans le groupe actif (appelant Owner, vérifié par [MinimumBandRole]).
    /// Une invitation encore en attente pour la même adresse est annulée : l'ancien lien ne marche plus.
    /// </summary>
    public async Task<InvitationCreatedDto> HandleAsync(CreateInvitationCommand command, CancellationToken ct)
    {
        var bandId = bandContext.RequiredBandId;
        var now = clock.GetUtcNow();
        var email = Invitation.NormalizeEmail(command.Email);
        var band = await bands.GetByIdAsync(bandId, ct) ?? throw new NotFoundException("Band", bandId);

        var members = await bands.ListMembersAsync(bandId, ct);
        if (members.Any(m => string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"{email} est déjà membre du groupe.");

        foreach (var previous in await invitations.ListPendingForEmailAsync(bandId, email, now, ct))
            previous.Revoke(now);

        var (token, hash) = InvitationToken.New();
        var invitation = Invitation.Create(bandId, email, command.Role, hash, currentUser.UserId, now);
        invitations.Add(invitation);
        await unitOfWork.SaveChangesAsync(ct);

        var link = $"{app.Value.PublicUrl.TrimEnd('/')}/invite/{token}";
        var message = InvitationEmail.Build(
            email, band.Name, currentUser.Email ?? "Un membre du groupe", invitation.Role, link, invitation.ExpiresAt);

        var emailSent = true;
        try
        {
            await emailSender.SendAsync(message, ct);
        }
        catch (EmailDeliveryException)
        {
            emailSent = false;      // l'invitation reste valable : l'Owner peut copier le lien (détail dans les journaux)
        }

        return new InvitationCreatedDto(invitation.Id, invitation.Email, invitation.Role, invitation.ExpiresAt, link, emailSent);
    }
}

public sealed class ListInvitationsHandler(IInvitationRepository invitations, IBandContext bandContext, TimeProvider clock)
{
    /// <summary>Invitations en attente du groupe actif (Owner uniquement).</summary>
    public Task<IReadOnlyList<InvitationDto>> HandleAsync(CancellationToken ct)
        => invitations.ListPendingAsync(bandContext.RequiredBandId, clock.GetUtcNow(), ct);
}

public sealed class RevokeInvitationHandler(
    IInvitationRepository invitations, IUnitOfWork unitOfWork, IBandContext bandContext, TimeProvider clock)
{
    /// <summary>Annule une invitation en attente du groupe actif. Déjà utilisée, annulée ou expirée : 410.</summary>
    public async Task HandleAsync(Guid invitationId, CancellationToken ct)
    {
        var invitation = await invitations.GetInBandAsync(bandContext.RequiredBandId, invitationId, ct)
                         ?? throw new NotFoundException("Invitation", invitationId);
        invitation.Revoke(clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class PreviewInvitationHandler(IInvitationRepository invitations, IBandRepository bands, TimeProvider clock)
{
    /// <summary>Aperçu pour la page /invite/{jeton}, sans connexion. Lien inconnu : 404 ; plus utilisable : 410.</summary>
    public async Task<InvitationPreviewDto> HandleAsync(string token, CancellationToken ct)
    {
        var invitation = await invitations.FindByTokenAsync(token, ct);
        invitation.EnsurePending(clock.GetUtcNow());
        var band = await bands.GetByIdAsync(invitation.BandId, ct) ?? throw new NotFoundException("Band", invitation.BandId);
        return new InvitationPreviewDto(band.Name, invitation.Email, invitation.Role, invitation.ExpiresAt);
    }
}

public sealed class AcceptInvitationHandler(
    IInvitationRepository invitations, IBandRepository bands, IUnitOfWork unitOfWork,
    ICurrentUser currentUser, TimeProvider clock)
{
    /// <summary>
    /// L'utilisateur connecté rejoint le groupe avec le rôle prévu. Invitation et adhésion partent dans le même
    /// SaveChanges (une transaction) ; le RowVersion fait échouer (409) une seconde acceptation simultanée.
    /// </summary>
    public async Task<BandSummaryDto> HandleAsync(string token, CancellationToken ct)
    {
        var invitation = await invitations.FindByTokenAsync(token, ct);
        var band = await bands.GetWithMembersAsync(invitation.BandId, ct)
                   ?? throw new NotFoundException("Band", invitation.BandId);
        var userId = currentUser.UserId;
        var now = clock.GetUtcNow();

        // Double clic, ou retour sur la page après coup : déjà fait par ce compte, on renvoie simplement le groupe
        if (invitation.AcceptedById == userId && band.HasMember(userId))
            return Summary(band, userId);

        invitation.EnsurePending(now);
        if (!invitation.IsFor(currentUser.Email))
            throw new ForbiddenException($"Cette invitation est destinée à {invitation.Email} : connectez-vous avec ce compte.");

        invitation.Accept(userId, now);
        if (!band.HasMember(userId))
            band.AddMember(userId, invitation.Role, now);       // déjà membre : il garde son rôle actuel
        await unitOfWork.SaveChangesAsync(ct);
        return Summary(band, userId);
    }

    private static BandSummaryDto Summary(Band band, string userId)
        => new(band.Id, band.Name, band.Memberships.Single(m => m.UserId == userId).Role);
}

internal static class InvitationRepositoryExtensions
{
    /// <summary>Invitation de ce lien ; 404 sinon. Le jeton n'est jamais recopié dans le message d'erreur.</summary>
    public static async Task<Invitation> FindByTokenAsync(this IInvitationRepository invitations, string token, CancellationToken ct)
        => await invitations.GetByTokenHashAsync(InvitationToken.Hash(token), ct)
           ?? throw new NotFoundException("Invitation", "lien");
}