using SongVault.Domain.Bands;

namespace SongVault.Domain.Tests.Bands;

public sealed class BandRoleTests
{
    [Theory]
    [InlineData(BandRole.Owner, BandRole.Owner, true)]
    [InlineData(BandRole.Owner, BandRole.Member, true)]
    [InlineData(BandRole.Owner, BandRole.Guest, true)]
    [InlineData(BandRole.Member, BandRole.Owner, false)]
    [InlineData(BandRole.Member, BandRole.Member, true)]
    [InlineData(BandRole.Member, BandRole.Guest, true)]
    [InlineData(BandRole.Guest, BandRole.Owner, false)]
    [InlineData(BandRole.Guest, BandRole.Member, false)]
    [InlineData(BandRole.Guest, BandRole.Guest, true)]
    public void Allows_respecte_la_hierarchie_Owner_Member_Guest(BandRole role, BandRole minimum, bool expected)
        => Assert.Equal(expected, role.Allows(minimum));
}