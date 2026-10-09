using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;

namespace SongVault.Api.Auth;

/// <summary>Exige que l'utilisateur soit membre du groupe {bandId} de la route ; sinon 404.</summary>
public sealed class BandScopedAttribute() : TypeFilterAttribute(typeof(BandScopedFilter));

// Filtre de ressource : il passe AVANT la liaison du modèle et la validation [ApiController],
// donc un non-membre reçoit 404 même si le corps envoyé est invalide, et le corps n'est jamais lu.
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

        band.Set(bandId, role);
        await next();
    }
}