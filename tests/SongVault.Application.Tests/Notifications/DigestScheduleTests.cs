using SongVault.Application.Notifications;

namespace SongVault.Application.Tests.Notifications;

public sealed class DigestScheduleTests
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    [Theory]
    [InlineData("2026-10-14T04:00:00Z", "2026-10-14T05:00:00Z")]   // 6 h à Paris : le jour même, à 7 h (UTC+2)
    [InlineData("2026-10-14T05:00:00Z", "2026-10-15T05:00:00Z")]   // 7 h pile : déjà passé, le lendemain
    [InlineData("2026-12-01T12:00:00Z", "2026-12-02T06:00:00Z")]   // en hiver, 7 h à Paris = 6 h UTC
    [InlineData("2026-10-24T12:00:00Z", "2026-10-25T06:00:00Z")]   // la nuit du passage à l'heure d'hiver
    public void Prochain_7_h_a_Paris(string now, string expected)
        => Assert.Equal(DateTimeOffset.Parse(expected), DigestSchedule.Next(DateTimeOffset.Parse(now), 7, Paris));

    [Fact]
    public void Une_heure_qui_n_existe_pas_passe_a_la_suivante()
        // 29 mars 2026 : à Paris, 2 h n'existe pas (on passe de 2 h à 3 h)
        => Assert.Equal(DateTimeOffset.Parse("2026-03-29T01:00:00Z"),
            DigestSchedule.Next(DateTimeOffset.Parse("2026-03-28T12:00:00Z"), 2, Paris));
}