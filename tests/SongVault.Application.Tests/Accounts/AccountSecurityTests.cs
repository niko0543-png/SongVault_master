using SongVault.Application.Accounts;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Accounts;

namespace SongVault.Application.Tests.Accounts;

[Trait("Category", "Security")]
public sealed class AccountSecurityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("203.0.113.57", "203.0.113.0/24")]
    [InlineData("::ffff:203.0.113.57", "203.0.113.0/24")]           // IPv4 vue par un serveur IPv6
    [InlineData("2001:db8:85a3:8d3:1319:8a2e:370:7348", "2001:db8:85a3::/48")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("pas une IP", null)]
    public void Network_ne_garde_que_le_reseau(string? ip, string? expected)
        => Assert.Equal(expected, ClientLabels.Network(ip));

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:131.0) Gecko/20100101 Firefox/131.0", "Firefox sur Windows")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0", "Edge sur Windows")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15", "Safari sur macOS")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1", "Safari sur iOS")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36", "Chrome sur Android")]
    [InlineData("curl/8.5.0", null)]
    [InlineData(null, null)]
    public void Device_resume_le_navigateur(string? userAgent, string? expected)
        => Assert.Equal(expected, ClientLabels.Device(userAgent));

    [Fact]
    public async Task L_historique_s_enregistre_aussitot_sans_l_IP_complete()
    {
        var events = new FakeAccountEventRepository();
        var uow = new FakeUnitOfWork();

        await new AccountSecurityLog(events, uow, new FixedTimeProvider(Now)).RecordAsync("ana", AccountEventType.SignInFailed,
            new ClientInfo("198.51.100.23", "Mozilla/5.0 (X11; Linux x86_64; rv:131.0) Gecko/20100101 Firefox/131.0"),
            CancellationToken.None);

        var e = Assert.Single(events.Events);
        Assert.Equal(("ana", AccountEventType.SignInFailed, Now), (e.UserId, e.Type, e.OccurredAt));
        Assert.Equal(("198.51.100.0/24", "Firefox sur Linux"), (e.Network, e.Device));
        Assert.Equal(1, uow.SaveCount);
    }
}