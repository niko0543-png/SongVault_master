using System.Diagnostics;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Observability;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Bands;
using SongVault.Domain.Common;
using SongVault.Domain.Invitations;

namespace SongVault.Api.ErrorHandling;

internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Règle métier non respectée"),
            NotFoundException => (StatusCodes.Status404NotFound, "Ressource introuvable"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Action non autorisée"),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Conflit de modification"),
            LastOwnerException => (StatusCodes.Status409Conflict, "Dernier propriétaire du groupe"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflit"),
            InvitationUnavailableException => (StatusCodes.Status410Gone, "Invitation indisponible"),
            LinkExpiredException => (StatusCodes.Status410Gone, "Lien expiré"),
            BadHttpRequestException bad => (bad.StatusCode, "Requête invalide"),
            UnsupportedFileException => (StatusCodes.Status400BadRequest, "Fichier refusé"),
            FileTooLargeException => (StatusCodes.Status413PayloadTooLarge, "Fichier trop volumineux"),
            FileNotReadableException => (StatusCodes.Status409Conflict, "Fichier indisponible"),
            PlanLimitException => (StatusCodes.Status402PaymentRequired, "Limite de la formule atteinte"),
            InvalidPaymentEventException => (StatusCodes.Status400BadRequest, "Événement de paiement refusé"),
            BillingUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Paiement indisponible"),
            _ => (StatusCodes.Status500InternalServerError, "Une erreur interne est survenue"),
        };

        if (status >= 500)
            // #18 : chemin sans jeton (lien de partage, invitation), aussi dans les journaux de la console
            logger.LogError(exception, "Erreur non gérée sur {Method} {Path}",
                httpContext.Request.Method, TelemetryRedactor.Redact(httpContext.Request.Path.ToString()));

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            // Jamais le message brut d'une exception inattendue : il peut contenir SQL, chemins, secrets…
            // Paiement indisponible (503) : message écrit pour l'utilisateur, sans détail de Stripe
            Detail = status >= 500 && exception is not BillingUnavailableException ? null : exception.Message,
        };
        // #18 : la référence que l'utilisateur peut donner ; c'est l'identifiant de la trace (operation_Id) dans Application Insights
        if (status >= 500 && Activity.Current is { } activity)
            problem.Extensions["reference"] = activity.TraceId.ToHexString();

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }
}