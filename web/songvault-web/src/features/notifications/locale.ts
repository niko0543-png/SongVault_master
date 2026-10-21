import type { Widen } from '@/shared/i18n/types'

/** #26 : textes de src/features/notifications (ADR 0033). Le français fait référence ; l'anglais a les mêmes clés. */
const fr = {
  section: {
    saved: 'Notifications enregistrées.',
    title: 'Notifications par e-mail',
    intro: 'Valable dans tous vos groupes. Vos propres modifications ne vous envoient jamais d’e-mail. Le résumé part chaque matin vers 7 h.',
    loading: 'Chargement…',
  },
  unsubscribe: {
    kindEmails: 'les e-mails « {title} »',
    noEmails: 'aucun e-mail de notification',
    title: 'Se désinscrire',
    question: 'Ne plus recevoir ce type d’e-mail de SongVault ?',
    working: 'Désinscription…',
    submit: 'Me désinscrire',
    done: 'C’est fait : vous ne recevrez plus {what}.',
    choose: 'Choisir mes notifications',
    invalid: 'Ce lien de désinscription est incomplet ou a été modifié. Choisissez vos notifications dans « Mon compte ».',
    goToAccount: 'Aller à « Mon compte »',
  },
  kinds: {
    NewVersion: {
      title: 'Nouvelle version',
      help: 'Un membre ajoute une version à un morceau du groupe.',
    },
    Comment: {
      title: 'Commentaires et réponses',
      help: 'Un membre commente un fichier ou répond à un commentaire.',
    },
    Mention: {
      title: 'Mentions',
      help: 'Un membre écrit « @ » suivi de votre adresse dans un commentaire.',
    },
  },
  modes: {
    Immediate: 'Tout de suite',
    Daily: 'Dans le résumé du matin',
    Off: 'Jamais',
  },
}

const en: Widen<typeof fr> = {
  section: {
    saved: 'Notifications saved.',
    title: 'E-mail notifications',
    intro: 'Applies to all your bands. Your own changes never send you an e-mail. The digest goes out every morning around 7 am.',
    loading: 'Loading…',
  },
  unsubscribe: {
    kindEmails: '“{title}” e-mails',
    noEmails: 'any notification e-mails',
    title: 'Unsubscribe',
    question: 'Stop receiving this type of e-mail from SongVault?',
    working: 'Unsubscribing…',
    submit: 'Unsubscribe me',
    done: 'Done: you will no longer receive {what}.',
    choose: 'Choose my notifications',
    invalid: 'This unsubscribe link is incomplete or has been altered. Choose your notifications in “My account”.',
    goToAccount: 'Go to “My account”',
  },
  kinds: {
    NewVersion: {
      title: 'New version',
      help: 'A member adds a version to one of the band’s songs.',
    },
    Comment: {
      title: 'Comments and replies',
      help: 'A member comments on a file or replies to a comment.',
    },
    Mention: {
      title: 'Mentions',
      help: 'A member writes “@” followed by your address in a comment.',
    },
  },
  modes: {
    Immediate: 'Right away',
    Daily: 'In the morning digest',
    Off: 'Never',
  },
}

export const notificationsMessages = { fr, en }
