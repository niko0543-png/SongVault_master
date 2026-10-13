using System.ComponentModel.DataAnnotations;

using SongVault.Application.Comments;
using SongVault.Domain.Comments;

namespace SongVault.Api.Contracts.Comments;

/// <summary>Nouveau commentaire. PositionMs : position dans l'enregistrement en millisecondes, absente pour un commentaire général.</summary>
public sealed record CreateCommentRequest(
    [Required, MaxLength(Comment.BodyMaxLength)] string Body,
    [Range(0, Comment.MaxPositionMs)] int? PositionMs);

/// <summary>Texte d'une réponse, ou nouveau texte d'un commentaire.</summary>
public sealed record CommentBodyRequest([Required, MaxLength(Comment.BodyMaxLength)] string Body);

/// <summary>
/// Un commentaire et, pour un commentaire d'origine, ses réponses (liste vide pour une réponse).
/// AuthorEmail est null quand le compte de l'auteur a été supprimé.
/// </summary>
public sealed record CommentResponse(
    Guid Id, Guid? ParentId, string? AuthorId, string? AuthorEmail, int? PositionMs, string Body,
    DateTimeOffset CreatedAt, DateTimeOffset? EditedAt, DateTimeOffset? ResolvedAt, string? ResolvedByEmail,
    IReadOnlyList<CommentResponse> Replies)
{
    public static CommentResponse From(CommentDto d, IEnumerable<CommentDto>? replies = null) =>
        new(d.Id, d.ParentId, d.AuthorId, d.AuthorEmail, d.PositionMs, d.Body,
            d.CreatedAt, d.EditedAt, d.ResolvedAt, d.ResolvedByEmail,
            [.. (replies ?? []).Select(r => From(r))]);

    public static CommentResponse From(CommentThreadDto thread) => From(thread.Comment, thread.Replies);
}