using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Bands;
using SongVault.Domain.Billing;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    private const long GiB = 1024L * 1024 * 1024;

    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("Plans");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasMaxLength(Plan.IdMaxLength).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(Plan.NameMaxLength).IsRequired();

        // Les formules de départ (ADR 0023). Les changer : une migration (UpdateData), jamais ce bloc seul
        builder.HasData(
            Plan.Create(Plan.FreeId, "Gratuit", 2 * GiB, maxBandsPerOwner: 1, monthlyPriceCents: 0, sortOrder: 1),
            Plan.Create("band", "Groupe", 50 * GiB, maxBandsPerOwner: null, monthlyPriceCents: 600, sortOrder: 2),
            Plan.Create("studio", "Studio", 250 * GiB, maxBandsPerOwner: null, monthlyPriceCents: 1500, sortOrder: 3));
    }
}

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions");
        builder.HasKey(s => s.BandId);                                  // un abonnement par groupe
        builder.Property(s => s.BandId).ValueGeneratedNever();
        builder.Property(s => s.PlanId).HasMaxLength(Plan.IdMaxLength).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.StripeCustomerId).HasMaxLength(Subscription.StripeIdMaxLength);
        builder.Property(s => s.StripeSubscriptionId).HasMaxLength(Subscription.StripeIdMaxLength);
        builder.HasIndex(s => s.StripeSubscriptionId).IsUnique().HasFilter("[StripeSubscriptionId] IS NOT NULL");

        // Effacé avec son groupe (purge d'un compte) ; une formule utilisée ne s'efface pas
        builder.HasOne<Band>().WithOne().HasForeignKey<Subscription>(s => s.BandId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Plan>().WithMany().HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProcessedStripeEventConfiguration : IEntityTypeConfiguration<ProcessedStripeEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedStripeEvent> builder)
    {
        builder.ToTable("ProcessedStripeEvents");
        builder.HasKey(e => e.EventId);                                 // un même événement ne passe qu'une fois
        builder.Property(e => e.EventId).HasMaxLength(ProcessedStripeEvent.EventIdMaxLength).ValueGeneratedNever();
        builder.Property(e => e.Type).HasMaxLength(ProcessedStripeEvent.TypeMaxLength).IsRequired();
    }
}