import type { Widen } from '@/shared/i18n/types'

/** #26 : textes de src/features/chords (ADR 0033). Le français fait référence ; l'anglais a les mêmes clés. */
const fr = {
  lyrics: {
    viewLabel: 'Affichage des paroles',
    read: 'Lecture',
    edit: 'Modifier le texte',
    transposition: 'Transposition',
    down: 'Descendre d’un demi-ton',
    downShort: '−½ ton',
    up: 'Monter d’un demi-ton',
    upShort: '+½ ton',
    reset: 'Revenir à l’original',
    lyrics: 'Paroles',
    help: 'Accords entre crochets, juste avant la syllabe : [Am]Je marche [F]seul. {c: Refrain} pour une indication.',
    empty: 'Pas encore de paroles.',
    saveInKey: 'Enregistrer dans cette tonalité',
    saveBlocked: 'Enregistrez ou annulez d’abord vos autres modifications.',
    original: '{key} (d’origine)',
    originalKey: 'Tonalité d’origine',
    semitones: '{n} demi-tons',
    saveInKeyTitle: 'Enregistrer les accords en {key} ?',
    saveBySemitonesTitle: 'Enregistrer les accords à {n} demi-tons ?',
    rewritten: 'Les accords des paroles sont réécrits.',
    rewrittenWithKey: 'Les accords des paroles sont réécrits et la tonalité de la version passe à {key}.',
    undo: 'Pour revenir en arrière, transposez de {back} et enregistrez à nouveau.',
  },
}

const en: Widen<typeof fr> = {
  lyrics: {
    viewLabel: 'Lyrics display',
    read: 'Read',
    edit: 'Edit the text',
    transposition: 'Transposition',
    down: 'Down a semitone',
    downShort: '−½ step',
    up: 'Up a semitone',
    upShort: '+½ step',
    reset: 'Back to the original',
    lyrics: 'Lyrics',
    help: 'Chords in square brackets, just before the syllable: [Am]I walk [F]alone. {c: Chorus} for a note.',
    empty: 'No lyrics yet.',
    saveInKey: 'Save in this key',
    saveBlocked: 'Save or cancel your other changes first.',
    original: '{key} (original)',
    originalKey: 'Original key',
    semitones: '{n} semitones',
    saveInKeyTitle: 'Save the chords in {key}?',
    saveBySemitonesTitle: 'Save the chords {n} semitones away?',
    rewritten: 'The chords in the lyrics are rewritten.',
    rewrittenWithKey: 'The chords in the lyrics are rewritten and the version key becomes {key}.',
    undo: 'To go back, transpose by {back} and save again.',
  },
}

export const chordsMessages = { fr, en }
