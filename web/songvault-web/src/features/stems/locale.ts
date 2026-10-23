import type { Widen } from '@/shared/i18n/types'

/** #26 : textes de src/features/stems (ADR 0033). Le français fait référence ; l'anglais a les mêmes clés. */
const fr = {
  mixer: {
    label: 'Mélangeur',
    pause: 'Mettre les pistes en pause',
    play: 'Lire les pistes',
    position: 'Position dans les pistes',
    mute: 'Couper {name}',
    muteShort: 'Muet',
    solo: 'Écouter {name} en solo',
    soloShort: 'Solo',
    volume: 'Volume de {name}',
  },
  editor: {
    nameRequired: 'Chaque piste a besoin d’un nom.',
    emptyHelp: 'Aucune piste choisie : le mélangeur sera retiré.',
    listLabel: 'Pistes du mélangeur',
    nameOf: 'Nom de la piste {fileName}',
    namePlaceholder: 'Voix, Guitare, Batterie…',
    removeItem: 'Retirer {fileName}',
    remove: 'Retirer',
    fileLabel: 'Fichier audio à ajouter',
    chooseFile: 'Choisir un fichier audio…',
    add: 'Ajouter une piste',
    max: '{stemsMax} pistes au plus par version.',
    save: 'Enregistrer les pistes',
  },
  section: {
    saved: 'Pistes enregistrées.',
    removed: 'Mélangeur retiré.',
    title: 'Pistes séparées',
    edit: 'Modifier les pistes',
    choose: 'Choisir les pistes',
    empty: 'Pas de pistes séparées pour cette version.',
    help: 'Envoyez un fichier audio par instrument (voix, guitare, batterie…), puis choisissez-les ici pour les écouter ensemble et régler chacun.',
    waiting: 'Pas encore jouables (analyse ou aperçu en cours, ou en échec dans Fichiers) : {name}.',
    tooLong: 'Trop long pour la mémoire de cet appareil : {totalSeconds} de pistes au total (environ {neededMb} Mo une fois décodées), {budget} au plus ici. Retirez une piste ou écoutez les fichiers un par un.',
    loading: 'Chargement… {loaded}/{count}',
    open: {
      one: 'Ouvrir le mélangeur ({count} piste)',
      other: 'Ouvrir le mélangeur ({count} pistes)',
    },
  },
  engine: {
    loadFailed: "La piste « {name} » n'a pas pu être chargée ({status}).",
    loadError: 'Chargement des pistes impossible.',
  },
}

const en: Widen<typeof fr> = {
  mixer: {
    label: 'Mixer',
    pause: 'Pause the tracks',
    play: 'Play the tracks',
    position: 'Position in the tracks',
    mute: 'Mute {name}',
    muteShort: 'Mute',
    solo: 'Play {name} solo',
    soloShort: 'Solo',
    volume: 'Volume of {name}',
  },
  editor: {
    nameRequired: 'Each track needs a name.',
    emptyHelp: 'No track chosen: the mixer will be removed.',
    listLabel: 'Mixer tracks',
    nameOf: 'Name of track {fileName}',
    namePlaceholder: 'Vocals, Guitar, Drums…',
    removeItem: 'Remove {fileName}',
    remove: 'Remove',
    fileLabel: 'Audio file to add',
    chooseFile: 'Choose an audio file…',
    add: 'Add a track',
    max: '{stemsMax} tracks at most per version.',
    save: 'Save the tracks',
  },
  section: {
    saved: 'Tracks saved.',
    removed: 'Mixer removed.',
    title: 'Separate tracks',
    edit: 'Edit the tracks',
    choose: 'Choose the tracks',
    empty: 'No separate tracks for this version.',
    help: 'Upload one audio file per instrument (vocals, guitar, drums…), then choose them here to play them together and adjust each one.',
    waiting: 'Not playable yet (scan or preview in progress, or failed in Files): {name}.',
    tooLong: 'Too long for this device’s memory: {totalSeconds} of tracks in total (about {neededMb} MB once decoded), {budget} at most here. Remove a track or play the files one by one.',
    loading: 'Loading… {loaded}/{count}',
    open: {
      one: 'Open the mixer ({count} track)',
      other: 'Open the mixer ({count} tracks)',
    },
  },
  engine: {
    loadFailed: 'The track “{name}” could not be loaded ({status}).',
    loadError: 'Could not load the tracks.',
  },
}

export const stemsMessages = { fr, en }
