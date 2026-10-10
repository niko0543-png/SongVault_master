using SongVault.Domain.Bands;
using SongVault.Domain.Common;
using SongVault.Domain.Invitations;

namespace SongVault.Domain.Tests.Invitations;

public sealed class InvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 10, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Hash = new byte[Invitation.TokenHashLength];

    private static Invitation New(BandRole role = BandRole.Member, string email = "Invite@Test.local")
        => Invitation.Create(Guid.CreateVersion7(), email, role, Hash, "owner-1", Now);

    [Fact]
    public void Create_normalise_l_adresse_et_expire_dans_7_jours()
    {
        var invitation = New(email: "  Invite@Test.local ");

        Assert.Equal("invite@test.local", invitation.Email);
        Assert.Equal(Now.AddDays(7), invitation.ExpiresAt);
        Assert.Equal(InvitationStatus.Pending, invitation.StatusAt(Now));
    }

    [Fact]
    public void Create_refuse_le_role_Owner()
        => Assert.Throws<DomainException>(() => New(BandRole.Owner));

    [Theory]
    [InlineData("")]
    [InlineData("pas-une-adresse")]
    [InlineData("@test.local")]
    [InlineData("a@b@c")]
    [InlineData("invite@")]
    public void Create_refuse_une_adresse_invalide(string email)
        => Assert.Throws<DomainException>(() => New(email: email));

    [Fact]
    public void Create_refuse_une_empreinte_qui_n_est_pas_un_SHA256()
        => Assert.Throws<ArgumentException>(
            () => Invitation.Create(Guid.CreateVersion7(), "a@b.c", BandRole.Member, new byte[10], "owner-1", Now));

    [Fact]
    public void Accept_enregistre_qui_et_quand()
    {
        var invitation = New();

        invitation.Accept("user-2", Now.AddHours(1));

        Assert.Equal("user-2", invitation.AcceptedById);
        Assert.Equal(InvitationStatus.Accepted, invitation.StatusAt(Now.AddHours(2)));
    }

    [Fact]
    public void Accept_refuse_une_seconde_fois()
    {
        var invitation = New();
        invitation.Accept("user-2", Now);

        var error = Assert.Throws<InvitationUnavailableException>(() => invitation.Accept("user-3", Now));
        Assert.Equal(InvitationStatus.Accepted, error.Status);
    }

    [Fact]
    public void Accept_refuse_apres_expiration()
    {
        var invitation = New();

        var error = Assert.Throws<InvitationUnavailableException>(() => invitation.Accept("user-2", Now + Invitation.Lifetime));
        Assert.Equal(InvitationStatus.Expired, error.Status);
    }

    [Fact]
    public void Accept_refuse_une_invitation_annulee()
    {
        var invitation = New();
        invitation.Revoke(Now);

        var error = Assert.Throws<InvitationUnavailableException>(() => invitation.Accept("user-2", Now));
        Assert.Equal(InvitationStatus.Revoked, error.Status);
    }

    [Theory]
    [InlineData("invite@test.local", true)]
    [InlineData("INVITE@test.LOCAL", true)]
    [InlineData("autre@test.local", false)]
    [InlineData(null, false)]
    public void IsFor_compare_les_adresses_sans_la_casse(string? email, bool expected)
        => Assert.Equal(expected, New().IsFor(email));
}