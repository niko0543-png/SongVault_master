using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Activity;
using SongVault.Domain.Bands;
using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class ActivityEventConfiguration : IEntityTypeConfiguration<ActivityEvent>
{
    public void Configure(EntityTypeBuilder<ActivityEvent> builder)
    {
        builder.ToTable("ActivityEvents");
        builder.HasKey(e => e.Id);                                             // bigint IDENTITY
        builder.Property(e => e.ActorId).HasMaxLength(BandMembership.UserIdMaxLength);
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(40);
        builder.OwnsOne(e => e.Details, d => d.ToJson());                      // une colonne JSON « Details »

        // Groupe effacé (purge d'un compte) : ses événements partent avec lui.
        // Pas de clé étrangère vers Songs : « Ana a supprimé Nocturne » reste dans le fil.
        builder.HasOne<Band>().WithMany()
               .HasForeignKey(e => e.BandId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.BandId, e.CreatedAt, e.Id });           // le fil, page par page
        builder.HasIndex(e => new { e.SongId, e.CreatedAt });                 // les badges de la liste
        builder.HasIndex(e => e.ActorId);                                      // purge d'un compte
        // #12 : les événements pas encore notifiés, sans parcourir tout le fil
        builder.HasIndex(e => e.Id).HasFilter("[NotifiedAt] IS NULL").HasDatabaseName("IX_ActivityEvents_NotNotified");
    }
}

internal sealed class SongViewConfiguration : IEntityTypeConfiguration<SongView>
{
    public void Configure(EntityTypeBuilder<SongView> builder)
    {
        builder.ToTable("SongViews");
        builder.HasKey(v => new { v.UserId, v.SongId });
        builder.Property(v => v.UserId).HasMaxLength(BandMembership.UserIdMaxLength);

        // Morceau supprimé : ses ouvertures partent avec lui. UserId sans clé étrangère, comme Comments.AuthorId :
        // la purge d'un compte les efface elle-même.
        builder.HasOne<Song>().WithMany()
               .HasForeignKey(v => v.SongId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(v => v.SongId);
    }
}