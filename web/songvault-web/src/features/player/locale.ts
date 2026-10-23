import type { Widen } from '@/shared/i18n/types'

/** #26 : textes de src/features/player (ADR 0033). Le français fait référence ; l'anglais a les mêmes clés. */
const fr = {
  audio: {
    label: 'Lecteur audio',
    back: 'Reculer de {skipSeconds} secondes',
    pause: 'Mettre en pause',
    play: 'Lire',
    pauseHint: 'Pause (Espace)',
    playHint: 'Lecture (Espace)',
    forward: 'Avancer de {skipSeconds} secondes',
    position: 'Position de lecture',
    volume: 'Volume',
    mute: 'Couper le son',
    unmute: 'Rétablir le son',
  },
  store: {
    cannotPlay: 'Impossible de lire ce fichier.',
    autoplayBlocked: 'Le navigateur a bloqué la lecture automatique : cliquez sur Lecture.',
    playFailed: 'Lecture impossible.',
  },
}

const en: Widen<typeof fr> = {
  audio: {
    label: 'Audio player',
    back: 'Back {skipSeconds} seconds',
    pause: 'Pause',
    play: 'Play',
    pauseHint: 'Pause (Space)',
    playHint: 'Play (Space)',
    forward: 'Forward {skipSeconds} seconds',
    position: 'Playback position',
    volume: 'Volume',
    mute: 'Mute',
    unmute: 'Unmute',
  },
  store: {
    cannotPlay: 'Cannot play this file.',
    autoplayBlocked: 'The browser blocked autoplay: click Play.',
    playFailed: 'Playback failed.',
  },
}

export const playerMessages = { fr, en }
