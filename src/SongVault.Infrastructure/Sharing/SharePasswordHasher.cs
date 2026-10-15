using Microsoft.AspNetCore.Identity;

using SongVault.Application.Abstractions;

namespace SongVault.Infrastructure.Sharing;

/// <summary>PasswordHasher d'Identity (PBKDF2, sel aléatoire), comme les mots de passe des comptes.</summary>
internal sealed class SharePasswordHasher : ISharePasswordHasher
{
    // Le hacheur par défaut n'utilise pas l'utilisateur : un objet quelconque suffit
    private static readonly object NoUser = new();
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(NoUser, password);

    public bool Verify(string passwordHash, string password)
        => _hasher.VerifyHashedPassword(NoUser, passwordHash, password) != PasswordVerificationResult.Failed;
}