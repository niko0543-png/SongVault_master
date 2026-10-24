namespace SongVault.Api.Contracts.Auth;

/// <summary>Lien de réinitialisation à vérifier dès son ouverture : adresse et code repris de l'URL.</summary>
public sealed record CheckResetCodeRequest(string? Email, string? ResetCode);
