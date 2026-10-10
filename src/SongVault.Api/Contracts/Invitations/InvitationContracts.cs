using System.ComponentModel.DataAnnotations;

using SongVault.Application.Invitations;
using SongVault.Domain.Bands;
using SongVault.Domain.Invitations;

namespace SongVault.Api.Contracts.Invitations;

/// <summary>Adresse à inviter et rôle (Member ou Guest ; Owner donne 422).</summary>
public sealed record CreateInvitationRequest(
    [Required, EmailAddress, MaxLength(Invitation.EmailMaxLength)] string Email,
    [Required] BandRole? Role);

public sealed record InvitationResponse(Guid Id, string Email, BandRole Role, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt)
{
    public static InvitationResponse From(InvitationDto dto) => new(dto.Id, dto.Email, dto.Role, dto.CreatedAt, dto.ExpiresAt);
}

/// <summary>Invitation créée. Link n'est renvoyé qu'ici : la base ne garde que l'empreinte du jeton.</summary>
public sealed record InvitationCreatedResponse(
    Guid Id, string Email, BandRole Role, DateTimeOffset ExpiresAt, string Link, bool EmailSent)
{
    public static InvitationCreatedResponse From(InvitationCreatedDto dto)
        => new(dto.Id, dto.Email, dto.Role, dto.ExpiresAt, dto.Link, dto.EmailSent);
}

public sealed record InvitationPreviewResponse(string BandName, string Email, BandRole Role, DateTimeOffset ExpiresAt)
{
    public static InvitationPreviewResponse From(InvitationPreviewDto dto) => new(dto.BandName, dto.Email, dto.Role, dto.ExpiresAt);
}