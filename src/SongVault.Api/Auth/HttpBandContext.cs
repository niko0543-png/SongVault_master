using SongVault.Application.Abstractions;
using SongVault.Domain.Bands;

namespace SongVault.Api.Auth;

/// <summary>Groupe actif de la requête HTTP en cours. Vide tant que [BandScoped] n'est pas passé.</summary>
internal sealed class HttpBandContext : IBandContext
{
    public Guid? BandId { get; private set; }
    public BandRole? Role { get; private set; }

    public Guid RequiredBandId => BandId
        ?? throw new InvalidOperationException("Aucun groupe actif dans la requête en cours.");

    /// <summary>Appelé uniquement par BandScopedFilter, après vérification de l'adhésion.</summary>
    internal void Set(Guid bandId, BandRole role) => (BandId, Role) = (bandId, role);
}