import type { TrashItem } from './types'

/** « Nocturne », « Nocturne — v2 Maquette », « Nocturne — v2 — prise.wav ». */
export function trashLabel(item: TrashItem): string {
  switch (item.kind) {
    case 'Song': return item.songTitle
    case 'Version': return `${item.songTitle} — v${item.versionNumber} ${item.versionTitle ?? ''}`.trim()
    case 'File': return `${item.songTitle} — v${item.versionNumber} — ${item.fileName}`
  }
}

/** « Morceau », « Version », « Fichier ». */
export function kindLabel(item: TrashItem): string {
  return { Song: 'Morceau', Version: 'Version', File: 'Fichier' }[item.kind]
}

/** Jours entiers avant la purge, arrondis vers le haut (0 : aujourd'hui). */
export function daysLeft(item: TrashItem, now: Date = new Date()): number {
  return Math.max(0, Math.ceil((new Date(item.purgeAt).getTime() - now.getTime()) / 86_400_000))
}