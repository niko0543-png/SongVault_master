import type { Widen } from '@/shared/i18n/types'

/** #26 : textes de src/features/status (ADR 0033). Le français fait référence ; l'anglais a les mêmes clés. */
const fr = {
  page: {
    allGood: 'Tout fonctionne',
    degraded: 'SongVault fonctionne, avec du retard',
    down: 'SongVault est en panne',
    noAnswer: 'SongVault ne répond pas',
    title: 'État du service',
    checking: 'Vérification…',
    apiDown: 'Le site s’affiche, mais le serveur ne répond pas. Nous sommes prévenus ; cette page se met à jour toute seule.',
    checked: 'Vérifié le {checkedAt}.',
    lastTry: 'Dernier essai le {lastTry}.',
    checkNow: 'Vérifier maintenant',
  },
  states: {
    ok: 'Opérationnel',
    degraded: 'Ralenti',
    down: 'En panne',
  },
}

const en: Widen<typeof fr> = {
  page: {
    allGood: 'All systems working',
    degraded: 'SongVault is working, with delays',
    down: 'SongVault is down',
    noAnswer: 'SongVault is not responding',
    title: 'Service status',
    checking: 'Checking…',
    apiDown: 'The site loads, but the server is not responding. We have been alerted; this page updates by itself.',
    checked: 'Checked on {checkedAt}.',
    lastTry: 'Last attempt on {lastTry}.',
    checkNow: 'Check now',
  },
  states: {
    ok: 'Operational',
    degraded: 'Slow',
    down: 'Down',
  },
}

export const statusMessages = { fr, en }
