using SongVault.Application.Comments;
using SongVault.Domain.Comments;

namespace SongVault.Application.Abstractions;

/// <summary>
/// Commentaires des fichiers audio du groupe actif. Un seul dépôt pour l'agrégat Comment (ADR 0006).
/// Un commentaire d'un autre groupe est « introuvable ».
/// </summary>
public interface ICommentRepository
{
    /// <summary>Ajoute un commentaire au contexte. N'écrit rien : l'appelant fait SaveChangesAsync.</summary>
    void Add(Comment comment);

    /// <summary>Retire le commentaire du contexte ; effacé au prochain SaveChangesAsync.</summary>
    void Remove(Comment comment);

    /// <summary>Commentaire suivi par EF s'il est sur ce fichier, dans le groupe actif ; sinon null.</summary>
    Task<Comment?> GetAsync(Guid fileId, Guid commentId, CancellationToken ct);

    /// <summary>Réponses à ce commentaire, suivies par EF (suppression d'un fil entier).</summary>
    Task<IReadOnlyList<Comment>> ListRepliesAsync(Guid commentId, CancellationToken ct);

    /// <summary>Tous les commentaires du fichier avec l'e-mail de leurs auteurs, du plus ancien au plus récent. Lecture sans suivi.</summary>
    Task<IReadOnlyList<CommentDto>> ListAsync(Guid fileId, CancellationToken ct);
}