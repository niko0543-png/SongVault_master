using System.ComponentModel.DataAnnotations;

using SongVault.Application.Files.Sync;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Api.Contracts.Files;

/// <summary>Un point : tel endroit de la tablature tombe à tel instant de la maquette (FlatSyncPoint d'alphaTab).</summary>
/// <param name="BarIndex">Mesure, à partir de 0.</param>
/// <param name="BarOccurence">Passage dans la mesure quand elle est reprise (0 = la première fois).</param>
/// <param name="BarPosition">Place dans la mesure, de 0 (son début) à 1 (sa fin).</param>
/// <param name="MillisecondOffset">Instant de la maquette, en millisecondes.</param>
public sealed record TabSyncPointRequest(
    [Required, Range(0, TabSyncPoint.MaxBarIndex)] int? BarIndex,
    [Required, Range(0, TabSyncPoint.MaxBarOccurence)] int? BarOccurence,
    [Required, Range(0.0, 1.0)] double? BarPosition,
    [Required, Range(0, TabSyncPoint.MaxMillisecondOffset)] int? MillisecondOffset);

/// <summary>Le calage complet, qui remplace l'ancien : la maquette suivie et tous les points, dans l'ordre de la lecture.</summary>
public sealed record SetTabSyncRequest(
    [Required] Guid? AudioFileId,
    [Required, MinLength(1), MaxLength(SongVersion.MaxSyncPoints)] IReadOnlyList<TabSyncPointRequest> Points)
{
    public SetTabSyncCommand ToCommand(Guid songId, Guid versionId, Guid fileId)
        => new(songId, versionId, fileId, AudioFileId!.Value,
            [.. Points.Select(p => new TabSyncPoint(p.BarIndex!.Value, p.BarOccurence!.Value, p.BarPosition!.Value, p.MillisecondOffset!.Value))]);
}

public sealed record TabSyncPointResponse(int BarIndex, int BarOccurence, double BarPosition, int MillisecondOffset);

/// <param name="AudioFileId">La maquette : un fichier audio de la même version, qui s'écoute par …/files/{id}/listen.</param>
public sealed record TabSyncResponse(Guid AudioFileId, string AudioFileName, IReadOnlyList<TabSyncPointResponse> Points)
{
    public static TabSyncResponse From(TabSyncDto d)
        => new(d.AudioFileId, d.AudioFileName,
            [.. d.Points.Select(p => new TabSyncPointResponse(p.BarIndex, p.BarOccurence, p.BarPosition, p.MillisecondOffset))]);
}