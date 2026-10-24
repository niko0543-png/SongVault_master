using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Bands;
using SongVault.Domain.Files;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class AudioSplitConfiguration : IEntityTypeConfiguration<AudioSplit>
{
    public void Configure(EntityTypeBuilder<AudioSplit> builder)
    {
        builder.ToTable("AudioSplits");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();                    // v7, vient du domaine
        builder.Property(s => s.SourceFileName).HasMaxLength(SongFile.OriginalFileNameMaxLength).IsRequired();
        builder.Property(s => s.RequestedById).HasMaxLength(BandMembership.UserIdMaxLength).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Error).HasMaxLength(AudioSplit.ErrorMaxLength);
        // Les passages, lus et écrits d'un bloc, tiennent dans une colonne JSON (comme les calages, ADR 0036)
        builder.Property(s => s.Parts)
            .HasConversion(
                parts => JsonSerializer.Serialize(parts, Json),
                json => JsonSerializer.Deserialize<List<AudioSplitPart>>(json, Json) ?? new List<AudioSplitPart>(),
                new ValueComparer<IReadOnlyList<AudioSplitPart>>(
                    (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
                    parts => parts.Aggregate(0, (hash, p) => HashCode.Combine(hash, p)),
                    parts => parts.ToList()))
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(s => new { s.SourceFileId, s.RequestedAt });          // « le dernier découpage du fichier »
        // Cascade : la purge du fichier emporte ses découpages. Pas de clé vers Bands ni vers le compte :
        // un seul chemin de cascade, et le compte supprimé ne bloque rien.
        builder.HasOne<SongFile>().WithMany()
               .HasForeignKey(s => s.SourceFileId)
               .OnDelete(DeleteBehavior.Cascade);
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
}
