import { STAFF_VIEWS, ZOOMS, type StaffView } from './display'

/**
 * #33 : réglages du lecteur gardés sur cet appareil (localStorage), jamais dans le fichier ni sur le serveur (ADR 0039).
 * Pour toutes les tablatures : zoom, portées, panneau des pistes replié. Par fichier : tempo et mix des pistes.
 * Un stockage bloqué (navigation privée) ou une valeur abîmée donne les réglages par défaut, sans erreur.
 */
const VIEW_KEY = 'songvault.tab.view'
const FILE_KEY = (fileId: string) => `songvault.tab.file.${fileId}`

export interface ViewPrefs { zoom: number; staffView: StaffView; panelOpen: boolean }
export interface FilePrefs { speed: number; mute: boolean[]; solo: boolean[]; volume: number[] }

export const DEFAULT_VIEW: ViewPrefs = { zoom: 1, staffView: 'file', panelOpen: true }

function read(key: string): Record<string, unknown> | null {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(key) ?? 'null')
    return value !== null && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : null
  } catch {
    return null
  }
}

function write(key: string, value: unknown) {
  try {
    localStorage.setItem(key, JSON.stringify(value))
  } catch {
    // stockage plein ou bloqué : le réglage vaut pour la séance seulement
  }
}

export function loadViewPrefs(): ViewPrefs {
  const stored = read(VIEW_KEY) ?? {}
  return {
    zoom: (ZOOMS as readonly number[]).includes(stored.zoom as number) ? stored.zoom as number : DEFAULT_VIEW.zoom,
    staffView: STAFF_VIEWS.includes(stored.staffView as StaffView) ? stored.staffView as StaffView : DEFAULT_VIEW.staffView,
    panelOpen: typeof stored.panelOpen === 'boolean' ? stored.panelOpen : DEFAULT_VIEW.panelOpen,
  }
}

export function saveViewPrefs(patch: Partial<ViewPrefs>) {
  write(VIEW_KEY, { ...loadViewPrefs(), ...patch })
}

const isList = <T>(value: unknown, item: (v: unknown) => v is T, length: number): value is T[] =>
  Array.isArray(value) && value.length === length && value.every(item)
const isBool = (v: unknown): v is boolean => typeof v === 'boolean'
const isVolume = (v: unknown): v is number => typeof v === 'number' && v >= 0 && v <= 2

/** Le mix n'est repris que s'il a autant de pistes que le fichier (un fichier remplacé repart à zéro). */
export function loadFilePrefs(fileId: string, trackCount: number, speed: { min: number; max: number }): Partial<FilePrefs> {
  const stored = read(FILE_KEY(fileId))
  if (!stored) return {}
  const prefs: Partial<FilePrefs> = {}
  if (typeof stored.speed === 'number' && stored.speed >= speed.min && stored.speed <= speed.max) prefs.speed = stored.speed
  if (isList(stored.mute, isBool, trackCount)) prefs.mute = stored.mute
  if (isList(stored.solo, isBool, trackCount)) prefs.solo = stored.solo
  if (isList(stored.volume, isVolume, trackCount)) prefs.volume = stored.volume
  return prefs
}

export function saveFilePrefs(fileId: string, prefs: FilePrefs) {
  write(FILE_KEY(fileId), prefs)
}
