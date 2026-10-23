import { STAFF_VIEWS, ZOOMS, type StaffView } from './display'

/**
 * #33 : réglages du lecteur gardés sur cet appareil (localStorage), jamais dans le fichier ni sur le serveur (ADR 0039).
 * Pour toutes les tablatures : zoom, portées, panneau des pistes replié. Par fichier : tempo et mix des pistes.
 * #35 : aussi la hauteur choisie pour le panneau des pistes, et les réglages dépliés ou non.
 * Volume général (piste « Général » du panneau) : le même pour toutes les tablatures, de 0 à 1.
 * Un stockage bloqué (navigation privée) ou une valeur abîmée donne les réglages par défaut, sans erreur.
 */
const VIEW_KEY = 'songvault.tab.view'
const FILE_KEY = (fileId: string) => `songvault.tab.file.${fileId}`

export interface ViewPrefs { zoom: number; staffView: StaffView; panelOpen: boolean; panelHeight: number | null; settingsOpen: boolean; masterVolume: number }
export interface FilePrefs { speed: number; mute: boolean[]; solo: boolean[]; volume: number[] }

export const DEFAULT_VIEW: ViewPrefs = { zoom: 1, staffView: 'file', panelOpen: true, panelHeight: null, settingsOpen: false, masterVolume: 1 }

/** #35 : hauteur du panneau des pistes, en px : au moins une piste, au plus un grand écran. */
export const PANEL_HEIGHT = { min: 48, max: 1600 } as const

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
    panelHeight: typeof stored.panelHeight === 'number' && stored.panelHeight >= PANEL_HEIGHT.min && stored.panelHeight <= PANEL_HEIGHT.max
      ? stored.panelHeight : DEFAULT_VIEW.panelHeight,
    settingsOpen: typeof stored.settingsOpen === 'boolean' ? stored.settingsOpen : DEFAULT_VIEW.settingsOpen,
    masterVolume: typeof stored.masterVolume === 'number' && stored.masterVolume >= 0 && stored.masterVolume <= 1
      ? stored.masterVolume : DEFAULT_VIEW.masterVolume,
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
