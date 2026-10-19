using System.ComponentModel.DataAnnotations;

using SongVault.Application.Files.Stems;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Api.Contracts.Files;

/// <summary>Une piste : un fichier audio de la version et son nom dans le mélangeur (« Voix », « Batterie »).</summary>
public sealed record StemRequest(
    [Required] Guid? FileId,
    [Required, MaxLength(SongFile.StemNameMaxLength)] string Name);

/// <summary>Toutes les pistes, dans l'ordre du mélangeur ; elles remplacent les anciennes. Liste vide = plus de mélangeur.</summary>
public sealed record SetStemsRequest([Required, MaxLength(SongVersion.MaxStems)] IReadOnlyList<StemRequest> Stems)
{
    public SetStemsCommand ToCommand(Guid songId, Guid versionId)
        => new(songId, versionId, [.. Stems.Select(s => new StemChoice(s.FileId!.Value, s.Name))]);
}

/// <param name="DurationMs">Durée de l'aperçu prêt, en millisecondes ; null tant qu'il se prépare.</param>
/// <param name="Playable">Fichier sain (#17) et aperçu MP3 prêt (#15) : le mélangeur peut le charger.</param>
public sealed record StemResponse(Guid FileId, string Name, int Position, string OriginalFileName, int? DurationMs, bool Playable)
{
    public static StemResponse From(StemDto d) => new(d.FileId, d.Name, d.Position, d.OriginalFileName, d.DurationMs, d.Playable);
}