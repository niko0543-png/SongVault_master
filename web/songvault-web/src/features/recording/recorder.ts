import { intlLocale, t } from '@/shared/i18n'

/**
 * Enregistrement d'une répétition depuis le navigateur (ADR 0049) : formats, noms et réglages, sans dépendance
 * à MediaRecorder pour rester testables. Le fichier produit suit le parcours de tout fichier audio (envoi direct,
 * quota, antivirus, aperçu MP3 et forme d'onde).
 */
export type RecordingExtension = '.webm' | '.m4a' | '.ogg'

export interface RecordingFormat {
  mimeType: string
  extension: RecordingExtension
}

/** Par ordre de préférence : Opus dans WebM (Chrome, Firefox, Edge), MP4 (Safari), Ogg (anciens Firefox). */
export const RECORDING_FORMATS: readonly RecordingFormat[] = [
  { mimeType: 'audio/webm;codecs=opus', extension: '.webm' },
  { mimeType: 'audio/webm', extension: '.webm' },
  { mimeType: 'audio/mp4', extension: '.m4a' },
  { mimeType: 'audio/ogg;codecs=opus', extension: '.ogg' },
]

/** 160 kbit/s : un groupe entier sans perte audible, environ 70 Mo par heure. */
export const RECORDING_BITS_PER_SECOND = 160_000

/** Le son arrive par blocs d'une seconde : pas de gros tampon unique à rendre d'un coup à l'arrêt. */
export const RECORDING_TIMESLICE_MS = 1000

/**
 * Micro réglé pour la musique : sans annulation d'écho, réduction de bruit ni gain automatique, faits pour la voix
 * d'un appel et qui écrasent un groupe (cymbales coupées, volume qui pompe).
 */
export function microphoneConstraints(deviceId: string | null): MediaTrackConstraints {
  return {
    ...(deviceId ? { deviceId: { exact: deviceId } } : {}),
    echoCancellation: false,
    noiseSuppression: false,
    autoGainControl: false,
    channelCount: { ideal: 2 },
  }
}

/** Premier format que le navigateur sait enregistrer, ou null (navigateur trop ancien). */
export function pickFormat(isTypeSupported: (mimeType: string) => boolean): RecordingFormat | null {
  return RECORDING_FORMATS.find((format) => isTypeSupported(format.mimeType)) ?? null
}

/** Extension d'après le type réellement produit (Safari peut ignorer celui demandé). */
export function extensionFor(mimeType: string, fallback: RecordingExtension): RecordingExtension {
  const type = mimeType.toLowerCase()
  if (type.startsWith('audio/webm') || type.startsWith('video/webm')) return '.webm'
  if (type.startsWith('audio/mp4') || type.startsWith('video/mp4')) return '.m4a'
  if (type.startsWith('audio/ogg')) return '.ogg'
  return fallback
}

const pad = (value: number) => String(value).padStart(2, '0')

/** « repetition-2026-10-24-2015.webm » : trié par date dans la liste des fichiers. */
export function recordingFileName(startedAt: Date, extension: RecordingExtension): string {
  const date = `${startedAt.getFullYear()}-${pad(startedAt.getMonth() + 1)}-${pad(startedAt.getDate())}`
  return `${t('recording.fileNamePrefix')}-${date}-${pad(startedAt.getHours())}${pad(startedAt.getMinutes())}${extension}`
}

/** Titre proposé pour le morceau ou la version créés : « Répétition du 24/10/2026 ». */
export function rehearsalTitle(startedAt: Date): string {
  const date = new Intl.DateTimeFormat(intlLocale(), { day: '2-digit', month: '2-digit', year: 'numeric' }).format(startedAt)
  return t('recording.defaultTitle', { date })
}

/** 75 000 ms → « 1:15 » ; 3 723 000 ms → « 1:02:03 » (une répétition dépasse souvent l'heure). */
export function formatElapsed(ms: number): string {
  const total = Math.max(0, Math.floor(ms / 1000))
  const hours = Math.floor(total / 3600)
  const minutes = Math.floor((total % 3600) / 60)
  const seconds = total % 60
  return hours > 0 ? `${hours}:${pad(minutes)}:${pad(seconds)}` : `${minutes}:${pad(seconds)}`
}

/** Crête d'un bloc d'échantillons (-1 à 1), ramenée entre 0 et 1. */
export function peakOf(samples: ArrayLike<number>): number {
  let peak = 0
  for (let i = 0; i < samples.length; i++) {
    const value = Math.abs(samples[i]!)
    if (value > peak) peak = value
  }
  return Math.min(1, peak)
}

/** Au-delà, le son sature : on prévient pour éloigner le téléphone ou baisser le gain de l'interface. */
export const CLIP_THRESHOLD = 0.99
