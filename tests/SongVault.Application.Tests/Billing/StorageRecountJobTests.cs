using SongVault.Application.Billing;

namespace SongVault.Application.Tests.Billing;

public sealed class StorageRecountJobTests
{
    [Theory]
    [InlineData("2026-10-16T00:30:00Z", "2026-10-16T01:00:00Z")]          // 2 h 30 à Paris (été) : 3 h le jour même
    [InlineData("2026-10-16T01:00:00Z", "2026-10-17T01:00:00Z")]          // pile 3 h : le lendemain
    [InlineData("2026-12-01T10:00:00Z", "2026-12-02T02:00:00Z")]          // hiver : 3 h = 2 h UTC
    public void Le_recalcul_passe_chaque_nuit_a_3_h_de_Paris(string now, string expected)
    {
        Assert.Equal(DateTimeOffset.Parse(expected), StorageRecountJob.NextRun(DateTimeOffset.Parse(now)));
    }
}