using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
        builder.HasIndex(s => s.Title);
        builder.HasMany(s => s.Versions)
       .WithOne()
       .HasForeignKey(v => v.SongId)
       .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}