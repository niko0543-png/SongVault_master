using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Bands;
using SongVault.Domain.Releases;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class ReleaseExportConfiguration : IEntityTypeConfiguration<ReleaseExport>
{
    public void Configure(EntityTypeBuilder<ReleaseExport> builder)
    {
        builder.ToTable("ReleaseExports");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();                    // v7, vient du domaine
        builder.Property(e => e.RequestedById).HasMaxLength(BandMembership.UserIdMaxLength).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(e => e.StorageKey);                                    // déduite de l'identifiant

        builder.HasIndex(e => new { e.ReleaseId, e.RequestedAt });            // « la dernière archive de la sortie »
        // Cascade : supprimer la sortie emporte ses archives ; la tâche d'expiration efface le fichier.
        // Pas de clé vers Bands ni vers le compte : un seul chemin de cascade, et le compte supprimé ne bloque rien.
        builder.HasOne<Release>().WithMany()
               .HasForeignKey(e => e.ReleaseId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}