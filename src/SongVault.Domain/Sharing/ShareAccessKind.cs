namespace SongVault.Domain.Sharing;

/// <summary>Ce qu'un visiteur a fait avec le lien : ouvert la page, lancé l'écoute d'un fichier, raté le mot de passe.</summary>
public enum ShareAccessKind { Opened, Played, WrongPassword }