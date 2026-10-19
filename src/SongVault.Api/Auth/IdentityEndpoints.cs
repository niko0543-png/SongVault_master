namespace SongVault.Api.Auth;

/// <summary>Routes de MapIdentityApi fermées (#19, ADR 0026) : leurs remplaçantes sont dans AccountSecurityController.</summary>
internal static class IdentityEndpoints
{
    /// <summary>
    /// /api/auth/manage/2fa désactive la double authentification sans redemander le mot de passe ;
    /// POST /api/auth/manage/info change le mot de passe sans l'écrire dans l'historique. GET manage/info reste ouvert.
    /// </summary>
    public static bool IsClosed(HttpRequest request)
        => request.Path.StartsWithSegments("/api/auth/manage/2fa", StringComparison.OrdinalIgnoreCase)
           || (HttpMethods.IsPost(request.Method)
               && request.Path.StartsWithSegments("/api/auth/manage/info", StringComparison.OrdinalIgnoreCase));
}