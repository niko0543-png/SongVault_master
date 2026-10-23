import type { Widen } from './types'

/** #26 : textes partagés par toute l'application (boutons, erreurs, tailles de fichier). */
const fr = {
  actions: {
    cancel: 'Annuler',
    save: 'Enregistrer',
    saving: 'Enregistrement…',
    delete: 'Supprimer',
    retry: 'Réessayer',
    close: 'Fermer',
    confirm: 'Confirmer',
  },
  language: {
    label: 'Langue',
    saveFailed: 'La langue n’a pas pu être enregistrée sur votre compte : elle reste choisie sur cet appareil.',
  },
  errors: {
    unreachable: "Le serveur est injoignable. Vérifiez que l'API est démarrée.",
    unreachableTitle: 'Serveur injoignable',
    server: 'Le serveur est indisponible ou a rencontré une erreur. Réessayez.',
    serverWithReference: 'Le serveur est indisponible ou a rencontré une erreur. Réessayez. Référence : {reference}',
    unexpected: 'Une erreur inattendue est survenue.',
    http: 'Erreur HTTP {status}',
  },
  fileSize: {
    b: 'o',
    kb: 'Ko',
    mb: 'Mo',
    gb: 'Go',
  },
}

const en: Widen<typeof fr> = {
  actions: {
    cancel: 'Cancel',
    save: 'Save',
    saving: 'Saving…',
    delete: 'Delete',
    retry: 'Try again',
    close: 'Close',
    confirm: 'Confirm',
  },
  language: {
    label: 'Language',
    saveFailed: 'The language could not be saved to your account: it stays selected on this device.',
  },
  errors: {
    unreachable: 'The server cannot be reached. Check that the API is running.',
    unreachableTitle: 'Server unreachable',
    server: 'The server is unavailable or ran into an error. Please try again.',
    serverWithReference: 'The server is unavailable or ran into an error. Please try again. Reference: {reference}',
    unexpected: 'An unexpected error occurred.',
    http: 'HTTP error {status}',
  },
  fileSize: {
    b: 'B',
    kb: 'KB',
    mb: 'MB',
    gb: 'GB',
  },
}

export const commonMessages = { fr, en }