import type { SongFile } from '@/features/files/types'
import { labels } from '@/shared/i18n'

// ---- Statuts (semaine 3) ----
export const SONG_VERSION_STATUSES = ['Idea', 'Demo', 'Arrangement', 'Rehearsal', 'Studio', 'Final'] as const
export type SongVersionStatus = (typeof SONG_VERSION_STATUSES)[number]

export const statusLabels = labels<SongVersionStatus>({
  Idea: 'versions.status.Idea',
  Demo: 'versions.status.Demo',
  Arrangement: 'versions.status.Arrangement',
  Rehearsal: 'versions.status.Rehearsal',
  Studio: 'versions.status.Studio',
  Final: 'versions.status.Final',
})

// ---- Limites : les MÊMES que le domaine C# ----
export const VERSION_TITLE_MAX = 200
export const VERSION_NOTES_MAX = 4000
export const VERSION_LYRICS_MAX = 20000
export const VERSION_BPM_MIN = 20                       // NOUVEAU (= SongVersion.MinBpm)
export const VERSION_BPM_MAX = 300                      // NOUVEAU (= SongVersion.MaxBpm)
/** Même règle que MusicalKey côté C#. Confort uniquement : le serveur reste l'autorité. */
export const MUSICAL_KEY_PATTERN = /^[A-G](#|b)?m?$/   // NOUVEAU
export const MUSICAL_KEY_MAX = 4                        // NOUVEAU (= MusicalKey.MaxLength)

// ---- Contrats API ----
export interface SongVersionSummary {
  id: string
  number: number
  title: string
  status: SongVersionStatus
  createdAt: string
  /** #34 : la première tablature lisible de la version ; null sinon */
  tablatureFileId?: string | null
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
  bpm: number | null                                    // NOUVEAU
  key: string | null                                    // NOUVEAU
  createdAt: string
  updatedAt: string
  files: SongFile[]
}

/** Corps de POST et PUT. PUT REMPLACE toute la version : chaque champ doit être fourni. */
export interface SongVersionInput {
  title: string
  status: SongVersionStatus
  notes: string | null
  lyrics: string | null
  bpm: number | null                                    // NOUVEAU
  key: string | null                                    // NOUVEAU
}

/**
 * Corps de POST (ADR 0047) : en plus des champs, des fichiers copiés depuis une autre version du morceau.
 * Les paroles, le BPM, la tonalité et les notes repris voyagent dans les champs eux-mêmes.
 */
export interface CreateSongVersionInput extends SongVersionInput {
  copyFromVersionId?: string | null
  copyFileIds?: string[]
}
