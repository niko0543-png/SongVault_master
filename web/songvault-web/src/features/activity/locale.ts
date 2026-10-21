import type { Widen } from '@/shared/i18n/types'

/** #26 : textes de src/features/activity (ADR 0033). Le français fait référence ; l'anglais a les mêmes clés. */
const fr = {
  feed: {
    title: 'Activité',
    empty: "Rien pour l'instant : les ajouts et modifications des membres apparaîtront ici.",
    listLabel: 'Activité du groupe',
    loading: 'Chargement…',
    more: 'Voir plus',
    end: "C'est le début du fil.",
  },
  describe: {
    you: 'Vous',
    deletedAccount: 'Compte supprimé',
    youDid: 'Vous avez {action}',
    theyDid: '{actor} a {action}',
    version: 'v{number} de {title}',
    aFile: 'un fichier',
    actions: {
      songCreated: 'créé le morceau',
      songUpdated: 'modifié le morceau',
      songDeleted: 'supprimé le morceau',
      versionAdded: 'ajouté la',
      versionUpdated: 'modifié la',
      fileAdded: 'ajouté {file} à la',
      fileRemoved: 'retiré {file} de la',
      commentAdded: 'commenté {file} dans la',
      commentReplied: 'répondu à un commentaire sur {file} dans la',
    },
  },
}

const en: Widen<typeof fr> = {
  feed: {
    title: 'Activity',
    empty: 'Nothing yet: what members add and change will show up here.',
    listLabel: 'Band activity',
    loading: 'Loading…',
    more: 'Show more',
    end: 'This is the beginning of the feed.',
  },
  describe: {
    you: 'You',
    deletedAccount: 'Deleted account',
    youDid: 'You {action}',
    theyDid: '{actor} {action}',
    version: '{title} v{number}',
    aFile: 'a file',
    actions: {
      songCreated: 'created the song',
      songUpdated: 'changed the song',
      songDeleted: 'deleted the song',
      versionAdded: 'added',
      versionUpdated: 'changed',
      fileAdded: 'added {file} to',
      fileRemoved: 'removed {file} from',
      commentAdded: 'commented on {file} in',
      commentReplied: 'replied to a comment on {file} in',
    },
  },
}

export const activityMessages = { fr, en }
