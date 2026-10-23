using SongVault.Domain.Common;

namespace SongVault.Domain.Releases;

/// <summary>Plateforme d'un lien d'écoute (#38), reconnue à l'adresse. Enregistrée en texte : en ajouter une ne décale rien.</summary>
public enum StreamingPlatform { Spotify, AppleMusic, Deezer, YouTubeMusic, YouTube, Bandcamp, SoundCloud, Tidal, AmazonMusic, Other }

/// <summary>
/// Un lien d'écoute d'une sortie (#38, ADR 0044) : l'adresse telle que la plateforme la donne, et la plateforme
/// reconnue à son nom de domaine. On accepte toute adresse http(s) ; une plateforme inconnue devient Other.
/// </summary>
public sealed record StreamingLink(StreamingPlatform Platform, string Url)
{
    public const int UrlMaxLength = 500;

    public static StreamingLink Parse(string? url)
    {
        var clean = Text.Required(url, UrlMaxLength, "lien d'écoute");
        if (!Uri.TryCreate(clean, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !uri.Host.Contains('.'))
            throw new DomainException($"Lien d'écoute invalide : « {clean} ». Collez l'adresse complète, qui commence par https://.");
        return new StreamingLink(Detect(uri.Host), clean);
    }

    /// <summary>La plateforme d'après le nom de domaine : music.youtube.com avant youtube.com, tout sous-domaine de bandcamp.com…</summary>
    public static StreamingPlatform Detect(string host)
    {
        var h = host.ToLowerInvariant();
        if (h.StartsWith("www.", StringComparison.Ordinal)) h = h[4..];
        bool Is(string domain) => h == domain || h.EndsWith("." + domain, StringComparison.Ordinal);

        if (Is("spotify.com") || Is("spotify.link")) return StreamingPlatform.Spotify;
        if (h is "music.apple.com" or "itunes.apple.com" or "geo.music.apple.com") return StreamingPlatform.AppleMusic;
        if (Is("deezer.com") || Is("deezer.page.link")) return StreamingPlatform.Deezer;
        if (h == "music.youtube.com") return StreamingPlatform.YouTubeMusic;
        if (Is("youtube.com") || h == "youtu.be") return StreamingPlatform.YouTube;
        if (Is("bandcamp.com")) return StreamingPlatform.Bandcamp;
        if (Is("soundcloud.com")) return StreamingPlatform.SoundCloud;
        if (Is("tidal.com")) return StreamingPlatform.Tidal;
        if (h.StartsWith("music.amazon.", StringComparison.Ordinal) || h.StartsWith("amazon.", StringComparison.Ordinal))
            return StreamingPlatform.AmazonMusic;
        return StreamingPlatform.Other;
    }
}
