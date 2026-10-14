namespace SongVault.Domain.Notifications;

/// <summary>
/// Ce qu'un e-mail annonce (#12). Enregistré en texte dans NotificationPreferences.Kind :
/// en ajouter un ne demande pas de migration.
/// </summary>
public enum NotificationKind
{
    /// <summary>Un autre membre a ajouté une version à un morceau du groupe.</summary>
    NewVersion,
    /// <summary>Un autre membre a commenté un fichier, ou répondu à un commentaire.</summary>
    Comment,
    /// <summary>Un autre membre a écrit « @votre-adresse » dans un commentaire ou une réponse.</summary>
    Mention,
}

/// <summary>Quand partent les e-mails d'un type.</summary>
public enum NotificationMode
{
    /// <summary>Jamais.</summary>
    Off,
    /// <summary>Un e-mail par événement, dans la minute.</summary>
    Immediate,
    /// <summary>Un seul e-mail le matin, qui regroupe la veille.</summary>
    Daily,
}