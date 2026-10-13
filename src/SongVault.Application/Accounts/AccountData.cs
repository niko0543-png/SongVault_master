using System.Text.Json.Serialization;

using SongVault.Domain.Bands;
using SongVault.Domain.Invitations;
using SongVault.Domain.Songs;

namespace SongVault.Application.Accounts;

/// <summary>Tout ce que le compte peut consulter, tel qu'écrit dans data.json (ADR 0011).</summary>
public sealed record AccountData(
    string UserId, string Email, bool EmailConfirmed,
    IReadOnlyList<AccountBandData> Bands,
    IReadOnlyList<AccountInvitationData> InvitationsSent,
    IReadOnlyList<AccountInvitationData> InvitationsReceived,
    IReadOnlyList<AccountCommentData> Comments);

public sealed record AccountBandData(
    Guid Id, string Name, BandRole Role, DateTimeOffset JoinedAt, IReadOnlyList<AccountSongData> Songs);

public sealed record AccountSongData(
    Guid Id, string Title, string? Artist, string? Description, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<AccountVersionData> Versions);

public sealed record AccountVersionData(
    Guid Id, int Number, string Title, SongVersionStatus Status, int? Bpm, string? Key, string? Notes, string? Lyrics,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<AccountFileData> Files);

public sealed record AccountFileData(
    Guid Id, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAt,
    [property: JsonIgnore] string StorageKey)
{
    /// <summary>Emplacement du fichier dans l'archive.</summary>
    public string Path => $"files/{Id}/{FileName}";
}

/// <summary>Un commentaire écrit par le compte, avec de quoi le situer (#8). ParentId : réponse à ce commentaire.</summary>
public sealed record AccountCommentData(
    Guid Id, string BandName, string SongTitle, int VersionNumber, string FileName,
    Guid? ParentId, int? PositionMs, string Body, DateTimeOffset CreatedAt, DateTimeOffset? EditedAt);

public sealed record AccountInvitationData(
    Guid BandId, string BandName, string Email, BandRole Role, DateTimeOffset CreatedAt, InvitationStatus Status);