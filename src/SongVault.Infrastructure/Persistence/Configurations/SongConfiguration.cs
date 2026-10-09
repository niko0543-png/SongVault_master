using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Bands;
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
        builder.Property<byte[]>("RowVersion").IsRowVersion();
    }
}