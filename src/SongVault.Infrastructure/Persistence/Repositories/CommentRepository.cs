using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Comments;
using SongVault.Domain.Comments;

namespace SongVault.Infrastructure.Persistence.Repositories;

/// <summary>Toutes les lectures partent de BandComments : seulement le groupe actif, vérifié par [BandScoped].</summary>
internal sealed class CommentRepository(SongVaultDbContext db, IBandContext bandContext) : ICommentRepository
{
    private IQueryable<Comment> BandComments => db.Comments.Where(c => c.BandId == bandContext.RequiredBandId);

    public void Add(Comment comment) => db.Comments.Add(comment);

    public void Remove(Comment comment) => db.Comments.Remove(comment);

    public Task<Comment?> GetAsync(Guid fileId, Guid commentId, CancellationToken ct)
        => BandComments.FirstOrDefaultAsync(c => c.SongFileId == fileId && c.Id == commentId, ct);

    public async Task<IReadOnlyList<Comment>> ListRepliesAsync(Guid commentId, CancellationToken ct)
        => await BandComments.Where(c => c.ParentId == commentId).ToListAsync(ct);

    public async Task<IReadOnlyList<CommentDto>> ListAsync(Guid fileId, CancellationToken ct)
        => await BandComments.AsNoTracking()
            .Where(c => c.SongFileId == fileId)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            // Auteur supprimé : AuthorId null, donc e-mail null (« Compte supprimé » à l'écran)
            .Select(c => new CommentDto(
                c.Id, c.ParentId, c.AuthorId,
                db.Users.Where(u => u.Id == c.AuthorId).Select(u => u.Email).FirstOrDefault(),
                c.PositionMs, c.Body, c.CreatedAt, c.EditedAt, c.ResolvedAt,
                db.Users.Where(u => u.Id == c.ResolvedById).Select(u => u.Email).FirstOrDefault()))
            .ToListAsync(ct);
}