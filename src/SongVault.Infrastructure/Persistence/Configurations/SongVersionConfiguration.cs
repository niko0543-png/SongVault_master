using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class SongVersionConfiguration : IEntityTypeConfiguration<SongVersion>
{
    public void Configure(EntityTypeBuilder<SongVersion> builder)
    {
        builder.ToTable("SongVersions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Title).HasMaxLength(SongVersion.TitleMaxLength).IsRequired();
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.Notes).HasMaxLength(SongVersion.NotesMaxLength);
        // Lyrics : pas de HasMaxLength → nvarchar(max) ; la limite est garantie par le domaine
        builder.HasIndex(v => new { v.SongId, v.Number }).IsUnique();
        builder.HasMany(v => v.Files)
       .WithOne()
       .HasForeignKey(f => f.SongVersionId)
       .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(v => v.Files).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}