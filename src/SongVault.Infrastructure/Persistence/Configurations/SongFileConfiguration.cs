using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Files;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class SongFileConfiguration : IEntityTypeConfiguration<SongFile>
{
    public void Configure(EntityTypeBuilder<SongFile> builder)
    {
        builder.ToTable("SongFiles");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.OriginalFileName).HasMaxLength(SongFile.OriginalFileNameMaxLength).IsRequired();
        builder.Property(f => f.StorageKey).HasMaxLength(100).IsRequired();
        builder.HasIndex(f => f.StorageKey).IsUnique();
        builder.Property(f => f.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(f => f.FileType).HasConversion<string>().HasMaxLength(20);
        // #20 : corbeille. Index filtré : la purge quotidienne ne lit que ce qui est à la corbeille
        builder.Property(f => f.DeletedById).HasMaxLength(450);
        builder.HasIndex(f => f.DeletedAt).HasFilter("[DeletedAt] IS NOT NULL");
        // #23 : piste du mélangeur, portée par le fichier (ADR 0030) ; les deux colonnes sont nulles ensemble
        builder.Property(f => f.StemName).HasMaxLength(SongFile.StemNameMaxLength);
        builder.ToTable(t => t.HasCheckConstraint("CK_SongFiles_Stem",
            "([StemName] IS NULL AND [StemPosition] IS NULL) OR ([StemName] IS NOT NULL AND [StemPosition] >= 0)"));
        // #29 : calage sur une maquette (ADR 0036). Les points, lus et écrits d'un bloc, tiennent dans une colonne JSON ;
        // pas de clé étrangère vers la maquette : mise seule à la corbeille puis purgée, elle laisse un calage ignoré
        builder.Property(f => f.SyncPoints)
            .HasConversion(
                points => points == null ? null : JsonSerializer.Serialize(points, Json),
                json => json == null ? null : JsonSerializer.Deserialize<List<TabSyncPoint>>(json, Json),
                new ValueComparer<IReadOnlyList<TabSyncPoint>?>(
                    (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
                    points => points == null ? 0 : points.Aggregate(0, (hash, p) => HashCode.Combine(hash, p)),
                    points => points == null ? null : points.ToList()))
            .HasColumnType("nvarchar(max)");
        builder.ToTable(t => t.HasCheckConstraint("CK_SongFiles_Sync",
            "([SyncAudioFileId] IS NULL AND [SyncPoints] IS NULL) OR ([SyncAudioFileId] IS NOT NULL AND [SyncPoints] IS NOT NULL)"));
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
}