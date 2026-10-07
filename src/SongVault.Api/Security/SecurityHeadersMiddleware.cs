namespace SongVault.Api.Security;

/// <summary>
/// Ajoute des en-têtes de sécurité à chaque réponse de l'API,
/// y compris aux réponses d'erreur produites plus loin dans le pipeline.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        // OnStarting : exécuté juste AVANT que les en-têtes partent vers le client
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // 1. Le navigateur ne « devine » jamais le type d'un fichier
            headers.XContentTypeOptions = "nosniff";

            // 2. Interdit d'afficher une réponse de SongVault dans une <iframe>
            headers.XFrameOptions = "DENY";

            // 3. L'adresse de la page n'est jamais transmise à un autre site
            headers["Referrer-Policy"] = "no-referrer";

            // 4. Les réponses de l'API ne sont jamais des pages : rien ne doit y être chargé ni exécuté.
            //    Limité à /api pour ne pas casser l'interface Scalar/OpenAPI en développement.
            if (context.Request.Path.StartsWithSegments("/api"))
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

            return Task.CompletedTask;
        });

        // Passe la main au maillon suivant (exception handler, contrôleurs…)
        return next(context);
    }
}