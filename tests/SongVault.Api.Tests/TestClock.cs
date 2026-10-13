namespace SongVault.Api.Tests;

/// <summary>Horloge que le test avance à la main (remplace TimeProvider.System dans un hôte dérivé).</summary>
public sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}