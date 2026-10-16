using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Domain.Billing;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests.Billing;

public static class BillingTestExtensions
{
    /// <summary>Offre directement en base une formule au groupe, comme le runbook le fait en SQL.</summary>
    public static async Task GrantPlanAsync(this SongVaultApiFactory factory, Guid bandId, string planId = "band")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
        db.Subscriptions.Add(Subscription.Grant(bandId, planId, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    /// <summary>Ajoute une formule minuscule (quota en octets) et l'offre au groupe : de quoi remplir un quota en un test.</summary>
    public static async Task GrantTinyPlanAsync(this SongVaultApiFactory factory, Guid bandId, long quotaBytes)
    {
        var planId = $"tiny-{Guid.NewGuid():N}"[..20];
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
            db.Plans.Add(Plan.Create(planId, "Minuscule", quotaBytes, maxBandsPerOwner: null, monthlyPriceCents: 0, sortOrder: 99));
            await db.SaveChangesAsync();
        }
        await factory.GrantPlanAsync(bandId, planId);
    }

    /// <summary>Stockage utilisé par le groupe, lu en base.</summary>
    public static async Task<long> StorageUsedAsync(this SongVaultApiFactory factory, Guid bandId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
        return await db.Bands.Where(b => b.Id == bandId).Select(b => b.StorageUsedBytes).SingleAsync();
    }
}