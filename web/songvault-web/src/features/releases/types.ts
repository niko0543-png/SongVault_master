import type { SongVersionStatus } from '@/features/versions/types'

export const RELEASE_TYPES = ['Single', 'EP', 'Album'] as const
export type ReleaseType = (typeof RELEASE_TYPES)[number]

export const releaseTypeLabels: Record<ReleaseType, string> = { Single: 'Single', EP: 'EP', Album: 'Album' }

// ---- Limites : les MÊMES que le domaine C# ----
export const RELEASE_TITLE_MAX = 200
export const RELEASE_MAX_TRACKS = 100
export const COVER_MAX_BYTES = 5 * 1024 * 1024
export const COVER_ACCEPT = '.jpg,.jpeg,.png,.webp'

// ---- Contrats API ----
export interface ReleaseSummary {
  id: string
  title: string
  type: ReleaseType
  /** "2026-11-20", ou null tant que la date n'est pas fixée */
  releaseDate: string | null
  hasCover: boolean
  trackCount: number
}

export interface ReleaseVersionOption {
  id: string
  number: number
  title: string
  status: SongVersionStatus
}

export interface ReleaseTrack {
  trackNumber: number
  songId: string
  songTitle: string
  versionId: string
  /** Toutes les versions du morceau, pour choisir celle de la sortie */
  versions: ReleaseVersionOption[]
}

export interface Release extends Omit<ReleaseSummary, 'trackCount'> {
  createdAt: string
  updatedAt: string
  tracks: ReleaseTrack[]
}

export interface ReleaseInput {
  title: string
  type: ReleaseType
  releaseDate: string | null
}

/** Une piste telle que l'API la reçoit : la place vient de l'ordre dans la liste. */
export interface TrackChoice {
  songId: string
  versionId: string
}