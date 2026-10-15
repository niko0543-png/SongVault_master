using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Application.Common;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Invitations;
using SongVault.Domain.Common;
using SongVault.Domain.Sharing;

namespace SongVault.Application.Sharing;

public sealed record CreateShareLinkCommand(
    ShareTargetType TargetType, Guid TargetId, string? Label, int LifetimeDays, string? Password);

public sealed class CreateShareLinkHandler(
    IShareLinkRepository links, IUnitOfWork unitOfWork, IBandContext bandContext, ICurrentUser currentUser,
    ISharePasswordHasher passwords, IOptions<AppOptions> app, TimeProvider clock)
{
    /// <summary>
    /// Crée un lien vers une version ou une sortie du groupe actif (Member ou Owner, vérifié par [MinimumBandRole]).
    /// Le lien complet n'est renvoyé qu'ici.
    /// </summary>
    public async Task<ShareLinkCreatedDto> HandleAsync(CreateShareLinkCommand command, CancellationToken ct)
    {
        var bandId = bandContext.RequiredBandId;
        if (!Enum.IsDefined(command.TargetType))
            throw new DomainException("Un lien de partage vise une version ou une sortie.");
        // Une cible d'un autre groupe est « introuvable », comme partout ailleurs
        if (!await links.TargetExistsAsync(bandId, command.TargetType, command.TargetId, ct))
            throw new NotFoundException(command.TargetType.ToString(), command.TargetId);

        // Le mot de passe n'est jamais rogné : des espaces voulus en font partie
        var password = string.IsNullOrEmpty(command.Password) ? null : command.Password;
        if (password is { Length: < ShareLink.PasswordMinLength or > ShareLink.PasswordMaxLength })
            throw new DomainException(
                $"Le mot de passe compte de {ShareLink.PasswordMinLength} à {ShareLink.PasswordMaxLength} caractères.");
        var passwordHash = password is null ? null : passwords.Hash(password);

        var now = clock.GetUtcNow();
        var (token, hash) = InvitationToken.New();          // même format que les invitations : 32 octets aléatoires
        var link = command.TargetType == ShareTargetType.Version
            ? ShareLink.ForVersion(bandId, command.TargetId, command.Label, command.LifetimeDays, hash, passwordHash, currentUser.UserId, now)
            : ShareLink.ForRelease(bandId, command.TargetId, command.Label, command.LifetimeDays, hash, passwordHash, currentUser.UserId, now);
        links.Add(link);
        await unitOfWork.SaveChangesAsync(ct);

        var dto = new ShareLinkDto(link.Id, link.TargetType, command.TargetId, link.Label, link.HasPassword,
            link.StatusAt(now), link.CreatedAt, link.ExpiresAt, null, currentUser.Email, 0, null);
        return new ShareLinkCreatedDto(dto, $"{app.Value.PublicUrl.TrimEnd('/')}/s/{token}");
    }
}

public sealed class ListShareLinksHandler(IShareLinkRepository links, IBandContext bandContext, TimeProvider clock)
{
    /// <summary>Tous les liens de cette version ou de cette sortie, actifs ou non, les plus récents d'abord.</summary>
    public Task<IReadOnlyList<ShareLinkDto>> HandleAsync(ShareTargetType type, Guid targetId, CancellationToken ct)
        => links.ListAsync(bandContext.RequiredBandId, type, targetId, clock.GetUtcNow(), ct);
}

public sealed class RevokeShareLinkHandler(
    IShareLinkRepository links, IUnitOfWork unitOfWork, IBandContext bandContext, TimeProvider clock)
{
    /// <summary>Coupe le lien tout de suite ; il reste dans la liste avec son journal. Recommencer ne change rien.</summary>
    public async Task HandleAsync(Guid linkId, CancellationToken ct)
    {
        var link = await links.GetInBandAsync(bandContext.RequiredBandId, linkId, ct)
                   ?? throw new NotFoundException("ShareLink", linkId);
        link.Revoke(clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class DeleteShareLinkHandler(IShareLinkRepository links, IUnitOfWork unitOfWork, IBandContext bandContext)
{
    /// <summary>Efface le lien et tout son journal. Un lien encore actif cesse aussitôt de marcher.</summary>
    public async Task HandleAsync(Guid linkId, CancellationToken ct)
    {
        var link = await links.GetInBandAsync(bandContext.RequiredBandId, linkId, ct)
                   ?? throw new NotFoundException("ShareLink", linkId);
        links.Remove(link);
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class ListShareLinkAccessesHandler(IShareLinkRepository links, IBandContext bandContext)
{
    /// <summary>Lignes montrées au plus : de quoi suivre un lien, sans charger un journal sans fin.</summary>
    public const int MaxEntries = 200;

    public async Task<IReadOnlyList<ShareLinkAccessDto>> HandleAsync(Guid linkId, CancellationToken ct)
    {
        var link = await links.GetInBandAsync(bandContext.RequiredBandId, linkId, ct)
                   ?? throw new NotFoundException("ShareLink", linkId);
        return await links.ListAccessesAsync(link.Id, MaxEntries, ct);
    }
}