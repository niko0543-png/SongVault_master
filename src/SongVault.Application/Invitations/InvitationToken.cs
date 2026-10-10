using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace SongVault.Application.Invitations;

public static class InvitationToken
{
    /// <summary>Nouveau jeton : 32 octets aléatoires en Base64Url (43 caractères), et son empreinte, seule stockée.</summary>
    public static (string Token, byte[] Hash) New()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    /// <summary>Empreinte SHA-256 du jeton tel qu'il apparaît dans le lien.</summary>
    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}