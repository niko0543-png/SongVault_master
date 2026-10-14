using SongVault.Application.Notifications;
using SongVault.Domain.Activity;

namespace SongVault.Application.Tests.Notifications;

internal static class NotificationTestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    public static readonly Guid BandId = Guid.Parse("0199e0a0-0000-7000-8000-000000000001");
    public static readonly Guid SongId = Guid.Parse("0199e0a0-0000-7000-8000-000000000002");
    public static readonly Guid VersionId = Guid.Parse("0199e0a0-0000-7000-8000-000000000003");

    public static NotificationRecipient Ana { get; } = new("ana", "ana@test.local");
    public static NotificationRecipient Bo { get; } = new("bo", "bo@test.local");
    public static NotificationRecipient Cy { get; } = new("cy", "cy@test.local");

    public static NotificationEvent VersionAdded(long id = 1, string actorId = "ana")
        => new(id, BandId, "Les Lunes", ActivityType.VersionAdded, actorId, $"{actorId}@test.local",
            SongId, VersionId, "Nocturne", 4, "Démo", null, null, Now);

    public static NotificationEvent Commented(string body, long id = 2, string actorId = "ana",
        ActivityType type = ActivityType.CommentAdded)
        => new(id, BandId, "Les Lunes", type, actorId, $"{actorId}@test.local",
            SongId, VersionId, "Nocturne", 4, "Démo", "prise-2.mp3", body, Now);
}