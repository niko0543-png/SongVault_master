using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Bands;
using SongVault.Domain.Comments;
using SongVault.Domain.Files;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();                    // v7, vient du domaine
        builder.Property(c => c.Body).HasMaxLength(Comment.BodyMaxLength).IsRequired();
        builder.Property(c => c.AuthorId).HasMaxLength(BandMembership.UserIdMaxLength);
        builder.Property(c => c.ResolvedById).HasMaxLength(BandMembership.UserIdMaxLength);

        // Fichier supprimé, ou sa version, ou son morceau : ses commentaires partent avec lui
        builder.HasOne<SongFile>().WithMany()
               .HasForeignKey(c => c.SongFileId)
               .OnDelete(DeleteBehavior.Cascade);

        // Pas de cascade sur les réponses : SQL Server refuse un second chemin de cascade depuis SongFiles.
        // DeleteCommentHandler efface les réponses avec leur fil.
        builder.HasOne<Comment>().WithMany()
               .HasForeignKey(c => c.ParentId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.SongFileId, c.CreatedAt });   // commentaires d'un fichier, dans l'ordre
        builder.HasIndex(c => c.AuthorId);                           // export et purge d'un compte

        // BandId sans clé étrangère, pour la même raison : un groupe effacé emporte déjà
        // ses commentaires par Songs → SongVersions → SongFiles → Comments.
    }
}