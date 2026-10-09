using SongVault.Domain.Bands;

namespace SongVault.Application.Abstractions;

/// <summary>Groupe sur lequel porte la requête en cours (lu dans la route, adhésion vérifiée).</summary>
public interface IBandContext
{
    Guid? BandId { get; }
    BandRole? Role { get; }

    /// <summary>Groupe actif ; lève une exception si la route n'en porte pas.</summary>
    Guid RequiredBandId { get; }
}