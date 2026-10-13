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
    IReadOnlyList<AccountInvitationData> InvitationsReceived);

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

public sealed record AccountInvitationData(
    Guid BandId, string BandName, string Email, BandRole Role, DateTimeOffset CreatedAt, InvitationStatus Status);