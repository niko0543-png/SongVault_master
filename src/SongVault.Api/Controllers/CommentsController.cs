using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Comments;
using SongVault.Application.Comments;
using SongVault.Domain.Bands;

namespace SongVault.Api.Controllers;

/// <summary>
/// Commentaires horodatés sur un fichier audio (ADR 0014). Ouverts à tout membre, Guest compris :
/// exception voulue à la règle « écrire = Member » de l'ADR 0008.
/// </summary>
[ApiController]
[Route("api/bands/{bandId:guid}/songs/{songId:guid}/versions/{versionId:guid}/files/{fileId:guid}/comments")]
[BandScoped, MinimumBandRole(BandRole.Guest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class CommentsController : ControllerBase
{
    /// <summary>Fils du fichier : commentaires généraux, puis commentaires dans l'ordre de leur position.</summary>
    /// <response code="200">Les fils, chacun avec ses réponses.</response>
    /// <response code="422">Le fichier n'est pas un fichier audio.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CommentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IReadOnlyList<CommentResponse>> List(
        Guid songId, Guid versionId, Guid fileId, [FromServices] ListCommentsHandler handler, CancellationToken ct)
        => [.. (await handler.HandleAsync(songId, versionId, fileId, ct)).Select(CommentResponse.From)];

    /// <summary>Commente le fichier, à une position ou en général.</summary>
    /// <response code="201">Commentaire créé.</response>
    /// <response code="400">Texte vide ou trop long, position hors limites.</response>
    /// <response code="422">Le fichier n'est pas un fichier audio.</response>
    [HttpPost]
    [ProducesResponseType<CommentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CommentResponse>> Create(
        Guid songId, Guid versionId, Guid fileId, CreateCommentRequest request,
        [FromServices] CreateCommentHandler handler, CancellationToken ct)
    {
        var comment = await handler.HandleAsync(
            new CreateCommentCommand(songId, versionId, fileId, request.Body, request.PositionMs), ct);
        return StatusCode(StatusCodes.Status201Created, CommentResponse.From(comment));
    }

    /// <summary>Répond à un commentaire d'origine.</summary>
    /// <response code="201">Réponse créée.</response>
    /// <response code="422">Le commentaire visé est lui-même une réponse.</response>
    [HttpPost("{commentId:guid}/replies")]
    [ProducesResponseType<CommentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CommentResponse>> Reply(
        Guid fileId, Guid commentId, CommentBodyRequest request,
        [FromServices] ReplyToCommentHandler handler, CancellationToken ct)
    {
        var reply = await handler.HandleAsync(fileId, commentId, request.Body, ct);
        return StatusCode(StatusCodes.Status201Created, CommentResponse.From(reply));
    }

    /// <summary>Modifie le texte d'un commentaire. Réservé à son auteur.</summary>
    /// <response code="204">Texte modifié.</response>
    /// <response code="403">Vous n'êtes pas l'auteur du commentaire.</response>
    [HttpPut("{commentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Edit(
        Guid fileId, Guid commentId, CommentBodyRequest request,
        [FromServices] EditCommentHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(fileId, commentId, request.Body, ct);
        return NoContent();
    }

    /// <summary>Supprime un commentaire ; un commentaire d'origine part avec ses réponses. Réservé à l'auteur et aux Owner.</summary>
    /// <response code="204">Commentaire supprimé.</response>
    /// <response code="403">Vous n'êtes ni l'auteur ni Owner du groupe.</response>
    [HttpDelete("{commentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(
        Guid fileId, Guid commentId, [FromServices] DeleteCommentHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(fileId, commentId, ct);
        return NoContent();
    }

    /// <summary>Marque le fil comme traité. Réservé à l'auteur du fil et aux Member et Owner.</summary>
    /// <response code="204">Fil résolu (ou déjà résolu).</response>
    /// <response code="403">Vous êtes Guest et n'avez pas ouvert ce fil.</response>
    /// <response code="422">Le commentaire est une réponse : seul le fil se résout.</response>
    [HttpPost("{commentId:guid}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Resolve(
        Guid fileId, Guid commentId, [FromServices] ResolveCommentHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(fileId, commentId, resolved: true, ct);
        return NoContent();
    }

    /// <summary>Rouvre un fil résolu. Mêmes droits que la résolution.</summary>
    /// <response code="204">Fil rouvert.</response>
    [HttpDelete("{commentId:guid}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reopen(
        Guid fileId, Guid commentId, [FromServices] ResolveCommentHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(fileId, commentId, resolved: false, ct);
        return NoContent();
    }
}