using SongVault.Domain.Accounts;
using SongVault.Domain.Common;

namespace SongVault.Domain.Tests.Accounts;

public sealed class AccountEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Record_garde_le_reseau_et_l_appareil_raccourcis()
    {
        var e = AccountEvent.Record("ana", AccountEventType.SignedInWithCode, "203.0.113.0/24", new string('x', 300), Now);

        Assert.Equal(("ana", AccountEventType.SignedInWithCode, Now, "203.0.113.0/24"), (e.UserId, e.Type, e.OccurredAt, e.Network));
        Assert.Equal(AccountEvent.DeviceMaxLength, e.Device!.Length);
    }

    [Fact]
    public void Record_accepte_un_client_inconnu()
    {
        var e = AccountEvent.Record("ana", AccountEventType.SignInFailed, null, "  ", Now);
        Assert.Null(e.Network);
        Assert.Null(e.Device);
    }

    [Fact]
    public void Record_refuse_une_ligne_sans_compte()
        => Assert.Throws<DomainException>(() => AccountEvent.Record(" ", AccountEventType.SignedIn, null, null, Now));
}