namespace SongVault.Api.Contracts.ServiceStatus;

/// <summary>
/// Page de statut (#18) : "ok", "degraded" (ça marche, avec du retard) ou "down" (répondu en 503).
/// Jamais de message d'erreur ni de détail technique : la page est publique.
/// </summary>
public sealed record StatusResponse(string Status, DateTimeOffset CheckedAt, IReadOnlyList<ComponentStatusResponse> Components);

/// <summary>Une partie du service, nommée pour un visiteur, et son état.</summary>
public sealed record ComponentStatusResponse(string Name, string Status);