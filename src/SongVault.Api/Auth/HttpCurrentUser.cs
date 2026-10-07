using System.Security.Claims;

using SongVault.Application.Abstractions;

namespace SongVault.Api.Auth;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string UserId =>
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Aucun utilisateur authentifié dans la requête en cours.");
}