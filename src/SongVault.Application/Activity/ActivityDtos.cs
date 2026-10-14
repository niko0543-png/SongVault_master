using System.Globalization;

using SongVault.Domain.Activity;

namespace SongVault.Application.Activity;

/// <summary>Une ligne du fil d'activité. ActorEmail null : compte supprimé.</summary>
public sealed record ActivityItemDto(
    long Id, ActivityType Type, string? ActorId, string? ActorEmail,
    Guid SongId, bool SongExists, Guid? VersionId, Guid? EntityId,
    string SongTitle, int? VersionNumber, string? VersionTitle, string? FileName,
    DateTimeOffset CreatedAt);

/// <summary>Une page du fil ; NextCursor null quand il n'y a plus rien de plus ancien.</summary>
public sealed record ActivityPage(IReadOnlyList<ActivityItemDto> Items, string? NextCursor);

/// <summary>
/// Position dans le fil : le dernier événement déjà affiché. La page suivante commence strictement après,
/// donc un événement arrivé entre-temps ne décale rien. Texte « ticks_id », opaque pour le front.
/// </summary>
public readonly record struct ActivityCursor(DateTimeOffset CreatedAt, long Id)
{
    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture, $"{CreatedAt.UtcTicks}_{Id}");

    public static bool TryParse(string? text, out ActivityCursor cursor)
    {
        cursor = default;
        var parts = text?.Split('_');
        if (parts is not [var ticks, var id]
            || !long.TryParse(ticks, NumberStyles.None, CultureInfo.InvariantCulture, out var t)
            || !long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var i)
            || t > DateTimeOffset.MaxValue.UtcTicks)
            return false;
        cursor = new ActivityCursor(new DateTimeOffset(t, TimeSpan.Zero), i);
        return true;
    }
}