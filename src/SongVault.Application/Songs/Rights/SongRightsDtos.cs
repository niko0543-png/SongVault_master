using SongVault.Domain.Songs;

namespace SongVault.Application.Songs.Rights;

/// <summary>
/// Les droits d'un morceau (#22) : l'ISWC de l'œuvre, les crédits dans l'ordre et leur total, et l'ISRC de chaque
/// version en place (l'enregistrement).
/// </summary>
public sealed record SongRightsDto(
    Guid SongId, string SongTitle, string? Iswc, IReadOnlyList<CreditDto> Credits, decimal TotalPercent,
    IReadOnlyList<VersionIsrcDto> Versions)
{
    public static SongRightsDto From(Song song)
    {
        var credits = song.Credits.Select(c => new CreditDto(c.Name, c.Role, c.SharePercent)).ToList();
        var versions = song.Versions.Where(v => !v.IsDeleted).OrderBy(v => v.Number).Select(VersionIsrcDto.From).ToList();
        return new(song.Id, song.Title, song.Iswc?.Value, credits, credits.Sum(c => c.SharePercent), versions);
    }
}

public sealed record CreditDto(string Name, CreditRole Role, decimal SharePercent);

/// <summary>Une version et son ISRC, en forme lisible (FR-Z03-26-00001) ; null s'il n'est pas renseigné.</summary>
public sealed record VersionIsrcDto(Guid Id, int Number, string Title, string? Isrc)
{
    public static VersionIsrcDto From(SongVersion v) => new(v.Id, v.Number, v.Title, v.Isrc?.Display);
}

/// <summary>L'ensemble des droits, qui remplace l'ancien. Iswc vide ou null : pas de code.</summary>
public sealed record SetSongRightsCommand(Guid SongId, string? Iswc, IReadOnlyList<CreditChoice> Credits);

/// <summary>ISRC d'une version. Vide ou null : on retire le code.</summary>
public sealed record SetVersionIsrcCommand(Guid SongId, Guid VersionId, string? Isrc);