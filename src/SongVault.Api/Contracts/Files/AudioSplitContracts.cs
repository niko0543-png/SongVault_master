using System.ComponentModel.DataAnnotations;

using SongVault.Application.Files.Splits;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Api.Contracts.Files;

/// <summary>Un passage gardé de l'enregistrement.</summary>
/// <param name="StartMs">Début, en millisecondes depuis le début du fichier.</param>
/// <param name="EndMs">Fin, en millisecondes ; au plus la durée du fichier.</param>
/// <param name="Title">Nom du nouveau morceau, ou du passage quand il va dans un morceau existant.</param>
/// <param name="SongId">Morceau existant du groupe qui reçoit une nouvelle version ; absent : un nouveau morceau.</param>
public sealed record AudioSplitPartRequest(
    [Required, Range(0, int.MaxValue)] int? StartMs,
    [Required, Range(0, int.MaxValue)] int? EndMs,
    [Required, MaxLength(Song.TitleMaxLength)] string Title,
    Guid? SongId);

public sealed record RequestAudioSplitRequest(
    [Required, MinLength(1), MaxLength(AudioSplit.MaxParts)] IReadOnlyList<AudioSplitPartRequest> Parts)
{
    public RequestAudioSplitCommand ToCommand(Guid songId, Guid versionId, Guid fileId)
        => new(songId, versionId, fileId,
            [.. Parts.Select(p => new AudioSplitPartCommand(p.StartMs!.Value, p.EndMs!.Value, p.Title, p.SongId))]);
}

/// <param name="CreatedSongId">Une fois le découpage fait : le morceau qui a reçu le passage.</param>
/// <param name="CreatedVersionId">Une fois le découpage fait : la version créée pour le passage.</param>
public sealed record AudioSplitPartResponse(int StartMs, int EndMs, string Title, Guid? SongId, Guid? CreatedSongId, Guid? CreatedVersionId);

/// <param name="Status">Pending (en cours), Done (fait) ou Failed (Error dit pourquoi).</param>
public sealed record AudioSplitResponse(Guid Id, AudioSplitStatus Status, string? Error, DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt, IReadOnlyList<AudioSplitPartResponse> Parts)
{
    public static AudioSplitResponse From(AudioSplitDto d) => new(d.Id, d.Status, d.Error, d.RequestedAt, d.CompletedAt,
        [.. d.Parts.Select(p => new AudioSplitPartResponse(p.StartMs, p.EndMs, p.Title, p.SongId, p.CreatedSongId, p.CreatedVersionId))]);
}
