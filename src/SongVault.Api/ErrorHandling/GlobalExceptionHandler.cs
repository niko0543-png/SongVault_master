using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

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
            InvitationUnavailableException => (StatusCodes.Status410Gone, "Invitation indisponible"),
            BadHttpRequestException bad => (bad.StatusCode, "Requête invalide"),
            UnsupportedFileException => (StatusCodes.Status400BadRequest, "Fichier refusé"),
            FileTooLargeException => (StatusCodes.Status413PayloadTooLarge, "Fichier trop volumineux"),
            _ => (StatusCodes.Status500InternalServerError, "Une erreur interne est survenue"),
        };

        if (status >= 500)
            logger.LogError(exception, "Erreur non gérée sur {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // Jamais le message brut d'une exception inattendue : il peut contenir SQL, chemins, secrets…
                Detail = status >= 500 ? null : exception.Message,
            },
        });
    }
}