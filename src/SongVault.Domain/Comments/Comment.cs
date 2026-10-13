using SongVault.Domain.Common;

namespace SongVault.Domain.Comments;

/// <summary>
/// Commentaire sur un fichier audio, à une position facultative (ADR 0014). Un fil = un commentaire
/// d'origine et ses réponses : un seul niveau de réponse, et seul le commentaire d'origine se résout.
/// </summary>
public sealed class Comment
{
    public const int BodyMaxLength = 2000;
    /// <summary>24 h : au-delà, ce n'est plus une position dans un enregistrement.</summary>
    public const int MaxPositionMs = 24 * 60 * 60 * 1000;

    private Comment() { } // réservé à EF Core

    public Guid Id { get; private set; }
    public Guid BandId { get; private set; }
    public Guid SongFileId { get; private set; }
    /// <summary>Commentaire d'origine pour une réponse ; null pour un commentaire d'origine.</summary>
    public Guid? ParentId { get; private set; }
    /// <summary>Identifiant Identity de l'auteur ; null une fois son compte purgé.</summary>
    public string? AuthorId { get; private set; }
    /// <summary>Position dans l'enregistrement, en millisecondes ; null pour un commentaire général.</summary>
    public int? PositionMs { get; private set; }
    public string Body { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? EditedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? ResolvedById { get; private set; }

    public bool IsReply => ParentId is not null;

    public static Comment Start(Guid bandId, Guid fileId, string authorId, int? positionMs, string body, DateTimeOffset now)
    {
        if (positionMs is < 0 or > MaxPositionMs)
            throw new DomainException("La position doit être comprise entre 0:00 et 24 heures.");
        return New(bandId, fileId, null, authorId, positionMs, body, now);
    }

    /// <summary>Répond à ce commentaire. Une réponse n'a pas de position : elle suit le commentaire d'origine.</summary>
    public Comment Reply(string authorId, string body, DateTimeOffset now)
    {
        if (IsReply) throw new DomainException("On répond au commentaire d'origine, pas à une réponse.");
        return New(BandId, SongFileId, Id, authorId, null, body, now);
    }

    public bool IsWrittenBy(string userId) => AuthorId is not null && AuthorId == userId;

    public void Edit(string body, DateTimeOffset now)
    {
        Body = Text.Required(body, BodyMaxLength, "commentaire");
        EditedAt = now;
    }

    /// <summary>Marque le fil comme traité. Sans effet s'il l'est déjà : la première résolution est gardée.</summary>
    public void Resolve(string userId, DateTimeOffset now)
    {
        EnsureThread();
        if (ResolvedAt is not null) return;
        ResolvedAt = now;
        ResolvedById = userId;
    }

    public void Reopen()
    {
        EnsureThread();
        ResolvedAt = null;
        ResolvedById = null;
    }

    private void EnsureThread()
    {
        if (IsReply) throw new DomainException("Seul le commentaire d'origine se résout ou se rouvre.");
    }

    private static Comment New(Guid bandId, Guid fileId, Guid? parentId, string authorId, int? positionMs,
        string body, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(authorId)) throw new DomainException("Un commentaire a un auteur.");

        return new Comment
        {
            Id = Guid.CreateVersion7(now),
            BandId = bandId,
            SongFileId = fileId,
            ParentId = parentId,
            AuthorId = authorId,
            PositionMs = positionMs,
            Body = Text.Required(body, BodyMaxLength, "commentaire"),
            CreatedAt = now,
        };
    }
}