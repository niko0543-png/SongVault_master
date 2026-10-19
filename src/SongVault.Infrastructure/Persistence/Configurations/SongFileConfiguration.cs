using Microsoft.EntityFrameworkCore;
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
    }
}