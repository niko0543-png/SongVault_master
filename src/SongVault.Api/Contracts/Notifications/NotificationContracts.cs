using System.ComponentModel.DataAnnotations;

using SongVault.Application.Notifications;
using SongVault.Domain.Notifications;

namespace SongVault.Api.Contracts.Notifications;

/// <summary>Le mode d'un type d'e-mail ; isDefault : vous n'avez encore rien choisi pour ce type.</summary>
public sealed record NotificationPreferenceResponse(NotificationKind Kind, NotificationMode Mode, bool IsDefault)
{
    public static NotificationPreferenceResponse From(NotificationPreferenceDto dto) => new(dto.Kind, dto.Mode, dto.IsDefault);
}

/// <summary>Un type et son nouveau mode.</summary>
public sealed record NotificationPreferenceRequest([Required] NotificationKind? Kind, [Required] NotificationMode? Mode);

/// <summary>Ce qu'a coupé le lien : un type, ou tous (kind null).</summary>
public sealed record UnsubscribeResponse(NotificationKind? Kind);