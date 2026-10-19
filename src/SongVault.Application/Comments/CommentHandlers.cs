using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Bands;
using SongVault.Domain.Comments;
using SongVault.Domain.Common;
using SongVault.Domain.Files;
using SongVault.Application.Activity;
using SongVault.Application.Files;
using SongVault.Application.Audit;
using SongVault.Domain.Audit;

namespace SongVault.Application.Comments;

// Tout membre, Guest compris, lit, écrit et répond (ADR 0014). Le reste se décide ici, commentaire par commentaire :
// modifier = l'auteur ; supprimer = l'auteur ou un Owner ; résoudre et rouvrir = l'auteur du fil ou un Member.

public sealed class ListCommentsHandler(ISongRepository songs, ICommentRepository comments)
{
    /// <summary>
    /// Fils du fichier : les commentaires généraux d'abord, puis ceux qui ont une position, dans l'ordre de l'écoute.
    /// </summary>
    public async Task<IReadOnlyList<CommentThreadDto>> HandleAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
    {
        await songs.RequireAudioFileAsync(songId, versionId, fileId, ct);
        var all = await comments.ListAsync(fileId, ct);                 // déjà du plus ancien au plus récent

        var replies = all.Where(c => c.ParentId is not null).ToLookup(c => c.ParentId!.Value);
        return [.. all.Where(c => c.ParentId is null)
            .OrderBy(c => c.PositionMs.HasValue).ThenBy(c => c.PositionMs).ThenBy(c => c.CreatedAt)
            .Select(c => new CommentThreadDto(c, [.. replies[c.Id]]))];
    }
}

public sealed class CreateCommentHandler(
    ISongRepository songs, ICommentRepository comments, IUnitOfWork unitOfWork,
    IBandContext band, ICurrentUser user, TimeProvider clock, ActivityLog activity)
{
    public async Task<CommentDto> HandleAsync(CreateCommentCommand command, CancellationToken ct)
    {
        var file = await songs.RequireAudioFileAsync(command.SongId, command.VersionId, command.FileId, ct);

        var now = clock.GetUtcNow();
        var comment = Comment.Start(band.RequiredBandId, command.FileId, user.UserId,
            command.PositionMs, command.Body, now);
        comments.Add(comment);
        activity.Commented(file, comment, now);
        await unitOfWork.SaveChangesAsync(ct);
        return CommentDto.From(comment, user.Email);
    }
}

public sealed class ReplyToCommentHandler(
    ISongRepository songs, ICommentRepository comments, IUnitOfWork unitOfWork, ICurrentUser user,
    TimeProvider clock, ActivityLog activity)
{
    public async Task<CommentDto> HandleAsync(
        Guid songId, Guid versionId, Guid fileId, Guid commentId, string body, CancellationToken ct)
    {
        var file = await songs.RequireAudioFileAsync(songId, versionId, fileId, ct);
        var parent = await comments.GetRequiredAsync(fileId, commentId, ct);
        var now = clock.GetUtcNow();
        var reply = parent.Reply(user.UserId, body, now);                   // réponse à une réponse : 422
        comments.Add(reply);
        activity.Commented(file, reply, now);
        await unitOfWork.SaveChangesAsync(ct);
        return CommentDto.From(reply, user.Email);
    }
}

public sealed class EditCommentHandler(
    ICommentRepository comments, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
{
    public async Task HandleAsync(Guid fileId, Guid commentId, string body, CancellationToken ct)
    {
        var comment = await comments.GetRequiredAsync(fileId, commentId, ct);
        if (!comment.IsWrittenBy(user.UserId))
            throw new ForbiddenException("Seul l'auteur d'un commentaire peut le modifier.");

        comment.Edit(body, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class DeleteCommentHandler(
    ICommentRepository comments, IUnitOfWork unitOfWork, IBandContext band, ICurrentUser user,
    AuditLog audit, TimeProvider clock)
{
    /// <summary>Supprime un commentaire ; celui d'origine part avec toutes ses réponses.</summary>
    public async Task HandleAsync(Guid fileId, Guid commentId, CancellationToken ct)
    {
        var comment = await comments.GetRequiredAsync(fileId, commentId, ct);
        if (!comment.IsWrittenBy(user.UserId) && band.Role != BandRole.Owner)
            throw new ForbiddenException("Seuls l'auteur et les Owner du groupe peuvent supprimer un commentaire.");

        var replies = 0;
        if (!comment.IsReply)
            foreach (var reply in await comments.ListRepliesAsync(comment.Id, ct))
            {
                comments.Remove(reply);
                replies++;
            }
        comments.Remove(comment);
        // #19 : qui a supprimé le commentaire de qui ; jamais son texte
        audit.Record(comment.BandId, AuditAction.CommentDeleted, comment.IsReply ? "Réponse" : "Commentaire",
            clock.GetUtcNow(), comment.AuthorId, replies > 0 ? $"avec {replies} réponse{(replies > 1 ? "s" : "")}" : null);
        await unitOfWork.SaveChangesAsync(ct);      // EF efface les réponses avant leur fil
    }
}

public sealed class ResolveCommentHandler(
    ICommentRepository comments, IUnitOfWork unitOfWork, IBandContext band, ICurrentUser user, TimeProvider clock)
{
    /// <param name="resolved">true : marquer le fil comme traité ; false : le rouvrir.</param>
    public async Task HandleAsync(Guid fileId, Guid commentId, bool resolved, CancellationToken ct)
    {
        var comment = await comments.GetRequiredAsync(fileId, commentId, ct);
        if (!comment.IsWrittenBy(user.UserId) && band.Role?.Allows(BandRole.Member) != true)
            throw new ForbiddenException("Seuls l'auteur du fil et les membres du groupe peuvent le résoudre ou le rouvrir.");

        if (resolved) comment.Resolve(user.UserId, clock.GetUtcNow());
        else comment.Reopen();
        await unitOfWork.SaveChangesAsync(ct);
    }
}

internal static class CommentLookups
{
    /// <summary>404 si le fichier n'est pas dans cette version, ce morceau et le groupe actif ; 422 s'il n'est pas audio.</summary>
    public static async Task<SongFileLabel> RequireAudioFileAsync(
         this ISongRepository songs, Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
    {
        var file = await songs.GetFileLabelAsync(songId, versionId, fileId, ct)
                   ?? throw new NotFoundException("SongFile", fileId);
        if (file.FileType != SongFileType.Audio)
            throw new DomainException("Seuls les fichiers audio se commentent.");
        return file;
    }

    public static async Task<Comment> GetRequiredAsync(
        this ICommentRepository comments, Guid fileId, Guid commentId, CancellationToken ct)
        => await comments.GetAsync(fileId, commentId, ct) ?? throw new NotFoundException("Comment", commentId);
}