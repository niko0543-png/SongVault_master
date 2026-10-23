import type { Widen } from '@/shared/i18n/types'

/** #26 : textes de src/features/trash (ADR 0033). Le français fait référence ; l'anglais a les mêmes clés. */
const fr = {
  page: {
    deletedAccount: 'un compte supprimé',
    purgeTomorrow: 'supprimé définitivement d’ici demain',
    restored: '« {item} » est de retour.',
    purgeTitle: 'Supprimer définitivement ?',
    purgeMessage: '« {item} » et tous ses fichiers seront effacés tout de suite. On ne pourra plus les restaurer.',
    purge: 'Supprimer définitivement',
    purged: '« {item} » est supprimé définitivement.',
    title: 'Corbeille',
    intro: 'Morceaux, versions et fichiers supprimés : ils se restaurent pendant 30 jours, puis partent pour de bon. Jusque-là, ils comptent dans le stockage du groupe.',
    empty: 'La corbeille est vide.',
    listLabel: 'Éléments de la corbeille',
    deletedBy: ' Supprimé par {item} le {deletedAt} ·',
    restore: 'Restaurer',
    daysLeft: 'encore {count} jours',
  },
  kinds: {
    Song: 'Morceau',
    Version: 'Version',
    File: 'Fichier',
  },
}

const en: Widen<typeof fr> = {
  page: {
    deletedAccount: 'a deleted account',
    purgeTomorrow: 'deleted for good by tomorrow',
    restored: '“{item}” is back.',
    purgeTitle: 'Delete for good?',
    purgeMessage: '“{item}” and all its files will be erased right away. They can no longer be restored.',
    purge: 'Delete for good',
    purged: '“{item}” is deleted for good.',
    title: 'Trash',
    intro: 'Deleted songs, versions and files: they can be restored for 30 days, then they are gone for good. Until then, they count in the band’s storage.',
    empty: 'The trash is empty.',
    listLabel: 'Trash items',
    deletedBy: ' Deleted by {item} on {deletedAt} ·',
    restore: 'Restore',
    daysLeft: '{count} days left',
  },
  kinds: {
    Song: 'Song',
    Version: 'Version',
    File: 'File',
  },
}

export const trashMessages = { fr, en }
