using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Bands;

namespace SongVault.Api.Auth;

/// <summary>
/// Exige que l'utilisateur soit membre du groupe {bandId} de la route (sinon 404)
/// et que son rôle suffise pour l'action (sinon 403, voir MinimumBandRoleAttribute).
/// </summary>
public sealed class BandScopedAttribute() : TypeFilterAttribute(typeof(BandScopedFilter));

// Filtre de ressource : il passe AVANT la liaison du modèle et la validation [ApiController],
// donc un non-membre reçoit 404 et un Guest 403 même si le corps envoyé est invalide.
internal sealed class BandScopedFilter(IBandRepository bands, ICurrentUser user, HttpBandContext band)
    : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (!Guid.TryParse(context.RouteData.Values["bandId"]?.ToString(), out var bandId)
            || await bands.GetRoleAsync(bandId, user.UserId, context.HttpContext.RequestAborted) is not { } role)
        {
            throw new NotFoundException("Band", bandId);   // même ProblemDetails 404 que le reste de l'API
        }

        var required = RequiredRole(context);
        if (!role.Allows(required))
            throw new ForbiddenException($"Votre rôle dans ce groupe ({role}) ne permet pas cette action : rôle minimal {required}.");

        band.Set(bandId, role);
        await next();
    }

    // Les métadonnées listent celles du contrôleur puis celles de l'action : la dernière est la plus précise.
    private static BandRole RequiredRole(ResourceExecutingContext context)
    {
        var explicitRole = context.ActionDescriptor.EndpointMetadata.OfType<MinimumBandRoleAttribute>().LastOrDefault();
        if (explicitRole is not null) return explicitRole.Role;

        var method = context.HttpContext.Request.Method;
        return HttpMethods.IsGet(method) || HttpMethods.IsHead(method) ? BandRole.Guest : BandRole.Member;
    }
}