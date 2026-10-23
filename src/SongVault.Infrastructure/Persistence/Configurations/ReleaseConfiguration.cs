using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Bands;
using SongVault.Domain.Releases;
using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class ReleaseConfiguration : IEntityTypeConfiguration<Release>
{
    public void Configure(EntityTypeBuilder<Release> builder)
    {
        builder.ToTable("Releases");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();                    // v7, vient du domaine
        builder.Property(r => r.Title).HasMaxLength(Release.TitleMaxLength).IsRequired();
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(10);
        builder.Property(r => r.CoverStorageKey).HasMaxLength(100);
        builder.Property(r => r.CoverContentType).HasMaxLength(50);
        builder.HasIndex(r => new { r.BandId, r.ReleaseDate });
        builder.HasOne<Band>().WithMany()
               .HasForeignKey(r => r.BandId)
               .OnDelete(DeleteBehavior.Restrict);   // comme Songs : la pochette est dans le stockage, pas en base

        builder.HasMany(r => r.Tracks)
               .WithOne()
               .HasForeignKey(t => t.ReleaseId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Tracks).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        // #38 : infos de sortie. Les liens d'écoute en JSON, comme les points de calage (#29) : lus et écrits ensemble
        builder.Property(r => r.Label).HasMaxLength(Release.LabelMaxLength);
        builder.Property(r => r.Upc)
               .HasConversion(u => u!.Value, s => Upc.Parse(s))
               .HasMaxLength(Upc.MaxLength);
        builder.Property(r => r.StreamingLinks)
               .HasConversion(
                   links => JsonSerializer.Serialize(links, Json),
                   json => JsonSerializer.Deserialize<List<StreamingLink>>(json, Json) ?? new List<StreamingLink>(),
                   new ValueComparer<IReadOnlyList<StreamingLink>>(
                       (a, b) => a!.SequenceEqual(b!),
                       links => links.Aggregate(0, (hash, l) => HashCode.Combine(hash, l)),
                       links => links.ToList()))
               .IsRequired();                       // la migration remplit les sorties existantes avec []
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}

internal sealed class ReleaseTrackConfiguration : IEntityTypeConfiguration<ReleaseTrack>
{
    public void Configure(EntityTypeBuilder<ReleaseTrack> builder)
    {
        builder.ToTable("ReleaseTracks");
        builder.HasKey(t => new { t.ReleaseId, t.SongId });            // un morceau au plus une fois par sortie
        builder.HasIndex(t => new { t.ReleaseId, t.TrackNumber }).IsUnique();
        builder.HasIndex(t => t.SongId);                                 // « sur quelles sorties est ce morceau ? »

        // Restrict, pas Cascade : SQL Server refuse deux chemins de cascade, et un morceau sur une sortie
        // ne disparaît pas en silence (DeleteSongHandler refuse avec un 409).
        builder.HasOne<Song>().WithMany()
               .HasForeignKey(t => t.SongId)
               .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SongVersion>().WithMany()
               .HasForeignKey(t => t.VersionId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}