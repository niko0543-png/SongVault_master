import type { Widen } from '@/shared/i18n/types'

/** #26 : textes de src/shared (ADR 0033). Le français fait référence ; l'anglais a les mêmes clés. */
const fr = {
  api: {
    noBand: 'Aucun groupe actif',
  },
  reorder: {
    moved: '{item} déplacé en position {to} sur {count}.',
    drag: 'Glisser pour déplacer',
    up: 'Monter {item}',
    down: 'Descendre {item}',
  },
  toast: {
    close: 'Fermer la notification',
  },
  notFound: {
    title: 'Page introuvable',
    text: "L'adresse demandée n'existe pas.",
    back: 'Retour aux morceaux',
  },
  loading: 'Chargement…',
  time: {
    positionOf: '{position} sur {duration}',
  },
}

const en: Widen<typeof fr> = {
  api: {
    noBand: 'No active band',
  },
  reorder: {
    moved: '{item} moved to position {to} of {count}.',
    drag: 'Drag to move',
    up: 'Move {item} up',
    down: 'Move {item} down',
  },
  toast: {
    close: 'Close the notification',
  },
  notFound: {
    title: 'Page not found',
    text: 'The requested address does not exist.',
    back: 'Back to the songs',
  },
  loading: 'Loading…',
  time: {
    positionOf: '{position} of {duration}',
  },
}

export const sharedMessages = { fr, en }
