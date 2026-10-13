using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Bands;
using SongVault.Domain.Files;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class FileUploadConfiguration : IEntityTypeConfiguration<FileUpload>
{
    public void Configure(EntityTypeBuilder<FileUpload> builder)
    {
        builder.ToTable("FileUploads");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.UserId).HasMaxLength(BandMembership.UserIdMaxLength).IsRequired();
        builder.Property(u => u.FileName).HasMaxLength(SongFile.OriginalFileNameMaxLength).IsRequired();
        builder.Property(u => u.StorageKey).HasMaxLength(100).IsRequired();
        builder.HasIndex(u => u.StorageKey).IsUnique();

        // Volontairement aucune clé étrangère : la ligne survit à la suppression du morceau ou du compte,
        // et ExpireFileUploadJob efface alors le fichier envoyé (ADR 0012).
    }
}