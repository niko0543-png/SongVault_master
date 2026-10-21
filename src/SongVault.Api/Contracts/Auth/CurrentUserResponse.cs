namespace SongVault.Api.Contracts.Auth;

/// <param name="Locale">#26 : « fr » ou « en » si l'utilisateur a choisi une langue, sinon null.</param>
public sealed record CurrentUserResponse(string Id, string Email, string? Locale = null);