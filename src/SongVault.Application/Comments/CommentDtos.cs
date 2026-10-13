using SongVault.Domain.Comments;

namespace SongVault.Application.Comments;

/// <summary>
/// Un commentaire tel que l'affiche l'application. AuthorEmail est null quand le compte de l'auteur
/// a été purgé ; ResolvedByEmail aussi pour celui qui a résolu le fil.
/// </summary>
public sealed record CommentDto(
    Guid Id, Guid? ParentId, string? AuthorId, string? AuthorEmail, int? PositionMs, string Body,
    DateTimeOffset CreatedAt, DateTimeOffset? EditedAt, DateTimeOffset? ResolvedAt, string? ResolvedByEmail)
{
    /// <summary>Commentaire qui vient d'être écrit par authorEmail (pas encore résolu).</summary>
    public static CommentDto From(Comment c, string? authorEmail)
        => new(c.Id, c.ParentId, c.AuthorId, authorEmail, c.PositionMs, c.Body, c.CreatedAt, c.EditedAt, null, null);
}

/// <summary>Un fil : le commentaire d'origine et ses réponses, de la plus ancienne à la plus récente.</summary>
public sealed record CommentThreadDto(CommentDto Comment, IReadOnlyList<CommentDto> Replies);

public sealed record CreateCommentCommand(Guid SongId, Guid VersionId, Guid FileId, string Body, int? PositionMs);