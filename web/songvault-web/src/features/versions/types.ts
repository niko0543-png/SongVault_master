import type { SongFile } from '@/features/files/types'

export const VERSION_TITLE_MAX = 200
export const VERSION_NOTES_MAX = 4000
export const VERSION_LYRICS_MAX = 20000

export const SONG_VERSION_STATUSES = [
  'Idea',
  'Demo',
  'Arrangement',
  'Rehearsal',
  'Studio',
  'Final',
] as const

/** Union dérivée du tableau : 'Idea' | 'Demo' | … (une seule source de vérité). */
export type SongVersionStatus = (typeof SONG_VERSION_STATUSES)[number]

/** Record<Union, …> : si un statut est ajouté à l'union, la compilation échoue tant qu'il n'a pas de libellé. */
export const statusLabels: Record<SongVersionStatus, string> = {
  Idea: 'Idée',
  Demo: 'Démo',
  Arrangement: 'Arrangement',
  Rehearsal: 'Répétition',
  Studio: 'Studio',
  Final: 'Finale',
}

export interface SongVersionSummary {
  id: string
  number: number
  title: string
  status: SongVersionStatus
  createdAt: string
}

/** Détail complet (GET /versions/{id}), reflet de SongVersionResponse. */
export interface SongVersion {
  id: string
  songId: string
  number: number
  title: string
  status: SongVersionStatus
  notes: string | null
  lyrics: string | null
  createdAt: string
  updatedAt: string
  files: SongFile[]
}

/** Corps de POST et PUT : le numéro n'y figure JAMAIS. */
export interface SongVersionInput {
  title: string
  status: SongVersionStatus
  notes: string | null
  lyrics: string | null
}
