using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Bands;
using SongVault.Domain.Releases;
using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class SongConfiguration : IEntityTypeConfiguration<Song>
{
    public void Configure(EntityTypeBuilder<Song> builder)
    {
        builder.ToTable("Songs");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();   // l'Id vient du domaine (v7)
        builder.Property(s => s.Title).HasMaxLength(Song.TitleMaxLength).IsRequired();
        builder.Property(s => s.Artist).HasMaxLength(Song.ArtistMaxLength);
        builder.Property(s => s.Description).HasMaxLength(Song.DescriptionMaxLength);
        builder.HasIndex(s => new { s.BandId, s.Title });
        builder.HasOne<Band>().WithMany()
               .HasForeignKey(s => s.BandId)
               .OnDelete(DeleteBehavior.Restrict);   // supprimer un groupe ne doit pas effacer des fichiers sans IFileStorageService
        builder.HasMany(s => s.Versions)
       .WithOne()
       .HasForeignKey(v => v.SongId)
       .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
        // #22 : crédits et ISWC (ADR 0029). Credits est calculée (triée par Position) : EF passe par le champ _credits
        builder.HasMany(s => s.Credits)
               .WithOne()
               .HasForeignKey(c => c.SongId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Credits).HasField("_credits").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(s => s.Iswc)
               .HasConversion(i => i!.Value, s => Iswc.Parse(s))
               .HasMaxLength(Iswc.MaxLength);
        builder.Property<byte[]>("RowVersion").IsRowVersion();
        // #20 : corbeille. Index filtré : la purge quotidienne ne lit que ce qui est à la corbeille
        builder.Property(s => s.DeletedById).HasMaxLength(450);
        builder.HasIndex(s => s.DeletedAt).HasFilter("[DeletedAt] IS NOT NULL");
        // #38 : la sortie où le morceau est paru. La supprimer le remet en travail (SET NULL), sans toucher à ses versions
        builder.HasOne<Release>().WithMany()
               .HasForeignKey(s => s.ReleaseId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}