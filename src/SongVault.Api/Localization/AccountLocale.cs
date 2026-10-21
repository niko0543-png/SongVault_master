using System.Security.Claims;

using Microsoft.AspNetCore.Identity;

namespace SongVault.Api.Localization;

/// <summary>
/// #26 : la langue choisie par l'utilisateur, gardée comme une revendication Identity « locale »
/// (table AspNetUserClaims : pas de migration). Effacée avec le compte.
/// </summary>
public static class AccountLocale
{
    public const string ClaimType = "locale";
    public static readonly IReadOnlyList<string> Supported = ["fr", "en"];

    public static async Task<string?> GetAsync(UserManager<IdentityUser> users, IdentityUser user)
        => (await users.GetClaimsAsync(user)).FirstOrDefault(c => c.Type == ClaimType)?.Value;

    public static async Task SetAsync(UserManager<IdentityUser> users, IdentityUser user, string locale)
    {
        var current = (await users.GetClaimsAsync(user)).Where(c => c.Type == ClaimType).ToList();
        if (current.Count == 1 && current[0].Value == locale) return;
        if (current.Count > 0) await users.RemoveClaimsAsync(user, current);
        var result = await users.AddClaimAsync(user, new Claim(ClaimType, locale));
        if (!result.Succeeded) throw new InvalidOperationException("La langue n'a pas pu être enregistrée sur le compte.");
    }
}
