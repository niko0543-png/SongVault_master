import type { Widen } from '@/shared/i18n/types'

/** #26 : textes de src/features/files (ADR 0033). Le français fait référence ; l'anglais a les mêmes clés. */
const fr = {
  upload: {
    refused: 'Envoi refusé',
  },
  list: {
    empty: 'Aucun fichier pour cette version.',
    scanning: 'Analyse antivirus en cours…',
    blocked: "Bloqué : l'antivirus a détecté une menace ({scanThreat}). Ce fichier ne peut être ni écouté ni téléchargé.",
    scanFailed: " L'analyse antivirus a échoué.",
    rescan: "Relancer l'analyse",
    previewPending: "Préparation de l'aperçu…",
    previewFailed: " Aperçu indisponible : l'écoute utilise le fichier original.",
    play: '▶ Écouter',
    download: 'Télécharger',
    showTablature: 'Ouvrir la tablature',
  },
  uploader: {
    tooLarge: 'Fichier trop volumineux pour le serveur.',
    storageRefused: "L'envoi a été refusé par le stockage (adresse expirée ?). Recommencez.",
    policyFailed: "Impossible de charger les règles d'envoi.",
    choose: 'Choisir un fichier',
    orDrop: ' ou glissez-le ici',
    progress: 'Envoi en cours',
    uploading: 'Envoi de {currentName}…',
    seePlans: 'Voir les formules',
  },
  waveform: {
    position: 'Position dans {label}',
  },
  validation: {
    empty: 'Le fichier est vide.',
    badType: 'Type de fichier non autorisé. Extensions acceptées : {allowedExtensions}.',
    tooLarge: 'Fichier trop volumineux ({size}). Maximum : {maxSizeBytes}.',
  },
}

const en: Widen<typeof fr> = {
  upload: {
    refused: 'Upload refused',
  },
  list: {
    empty: 'No files for this version.',
    scanning: 'Virus scan in progress…',
    blocked: 'Blocked: the antivirus detected a threat ({scanThreat}). This file can be neither played nor downloaded.',
    scanFailed: ' The virus scan failed.',
    rescan: 'Run the scan again',
    previewPending: 'Preparing the preview…',
    previewFailed: ' Preview unavailable: playback uses the original file.',
    play: '▶ Listen',
    download: 'Download',
    showTablature: 'Open the tablature',
  },
  uploader: {
    tooLarge: 'File too large for the server.',
    storageRefused: 'The upload was refused by the storage (expired address?). Start again.',
    policyFailed: 'Could not load the upload rules.',
    choose: 'Choose a file',
    orDrop: ' or drop it here',
    progress: 'Upload in progress',
    uploading: 'Uploading {currentName}…',
    seePlans: 'See the plans',
  },
  waveform: {
    position: 'Position in {label}',
  },
  validation: {
    empty: 'The file is empty.',
    badType: 'File type not allowed. Accepted extensions: {allowedExtensions}.',
    tooLarge: 'File too large ({size}). Maximum: {maxSizeBytes}.',
  },
}

export const filesMessages = { fr, en }
