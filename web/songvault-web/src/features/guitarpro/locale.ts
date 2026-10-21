import type { Widen } from '@/shared/i18n/types'

/** #28 : textes du lecteur de tablature (ADR 0034). Le français fait référence ; l'anglais a les mêmes clés. */
const fr = {
  viewer: {
    label: 'Tablature de {label}',
    loading: 'Chargement de la tablature…',
    loadFailed: 'Impossible d’ouvrir ce fichier Guitar Pro.',
    soundLoading: 'Chargement des sons…',
    play: 'Lire la tablature',
    pause: 'Mettre en pause',
    stop: 'Revenir au début',
    tempo: 'Tempo',
    loop: 'Boucler la sélection',
    loopHint: 'Glissez sur la partition pour choisir des mesures : la lecture se limite à la sélection, et « Boucler » la répète.',
    transpose: 'Transposer',
    transposeDown: 'Un demi-ton plus bas',
    transposeUp: 'Un demi-ton plus haut',
    semitones: { zero: 'Tonalité du fichier', one: '{count} demi-ton', other: '{count} demi-tons' },
    tracks: 'Pistes',
    show: 'Afficher {name}',
    mute: 'Muet',
    solo: 'Solo',
    muteTrack: 'Couper {name}',
    soloTrack: 'Écouter {name} seule',
    readOnly: 'Lecture seule : le fichier n’est jamais modifié. Pour le changer, ouvrez-le dans Guitar Pro et envoyez-le dans une nouvelle version.',
  },
}

const en: Widen<typeof fr> = {
  viewer: {
    label: 'Tablature of {label}',
    loading: 'Loading the tablature…',
    loadFailed: 'This Guitar Pro file could not be opened.',
    soundLoading: 'Loading the sounds…',
    play: 'Play the tablature',
    pause: 'Pause',
    stop: 'Back to the start',
    tempo: 'Tempo',
    loop: 'Loop the selection',
    loopHint: 'Drag across the score to pick bars: playback stays within the selection, and “Loop” repeats it.',
    transpose: 'Transpose',
    transposeDown: 'One semitone down',
    transposeUp: 'One semitone up',
    semitones: { zero: 'Key of the file', one: '{count} semitone', other: '{count} semitones' },
    tracks: 'Tracks',
    show: 'Show {name}',
    mute: 'Mute',
    solo: 'Solo',
    muteTrack: 'Mute {name}',
    soloTrack: 'Play {name} alone',
    readOnly: 'Read only: the file is never changed. To change it, open it in Guitar Pro and upload it to a new version.',
  },
}

export const guitarproMessages = { fr, en }