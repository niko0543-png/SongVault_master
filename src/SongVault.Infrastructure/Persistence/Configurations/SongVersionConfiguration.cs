using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class SongVersionConfiguration : IEntityTypeConfiguration<SongVersion>
{
    public void Configure(EntityTypeBuilder<SongVersion> builder)
    {
        // Nom de table + contrainte CHECK : la base refuse un BPM invalide même hors application
        builder.ToTable("SongVersions", t => t.HasCheckConstraint(
            "CK_SongVersions_Bpm",
            $"[Bpm] IS NULL OR [Bpm] BETWEEN {SongVersion.MinBpm} AND {SongVersion.MaxBpm}"));

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Title).HasMaxLength(SongVersion.TitleMaxLength).IsRequired();
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.Notes).HasMaxLength(SongVersion.NotesMaxLength);

        // NOUVEAU : MusicalKey (objet) <-> nvarchar(4) (colonne). EF ne passe jamais null au convertisseur.
        builder.Property(v => v.Key)
               .HasConversion(k => k!.Value, s => MusicalKey.Parse(s))
               .HasMaxLength(MusicalKey.MaxLength);
        // Bpm (int?) : rien à configurer, EF crée une colonne int NULL

        builder.HasIndex(v => new { v.SongId, v.Number }).IsUnique();

        builder.HasMany(v => v.Files)
               .WithOne()
               .HasForeignKey(f => f.SongVersionId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(v => v.Files).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}