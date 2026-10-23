import type { SongVersionStatus } from '@/features/versions/types'

export const RELEASE_TYPES = ['Single', 'EP', 'Album'] as const
export type ReleaseType = (typeof RELEASE_TYPES)[number]

export const releaseTypeLabels: Record<ReleaseType, string> = { Single: 'Single', EP: 'EP', Album: 'Album' }

// ---- Limites : les MÊMES que le domaine C# ----
export const RELEASE_TITLE_MAX = 200
export const RELEASE_MAX_TRACKS = 100
export const COVER_MAX_BYTES = 5 * 1024 * 1024
export const COVER_ACCEPT = '.jpg,.jpeg,.png,.webp'
/** #38 : infos de sortie (= Release.LabelMaxLength, MaxStreamingLinks, StreamingLink.UrlMaxLength) */
export const RELEASE_LABEL_MAX = 200
export const RELEASE_MAX_STREAMING_LINKS = 12
export const STREAMING_URL_MAX = 500
export const UPC_INPUT_MAX = 30

/** #38 : plateforme d'un lien d'écoute, reconnue par l'API à l'adresse. Les noms de marque ne se traduisent pas. */
export type StreamingPlatform =
  | 'Spotify' | 'AppleMusic' | 'Deezer' | 'YouTubeMusic' | 'YouTube'
  | 'Bandcamp' | 'SoundCloud' | 'Tidal' | 'AmazonMusic' | 'Other'

export const streamingPlatformNames: Record<Exclude<StreamingPlatform, 'Other'>, string> = {
  Spotify: 'Spotify', AppleMusic: 'Apple Music', Deezer: 'Deezer', YouTubeMusic: 'YouTube Music', YouTube: 'YouTube',
  Bandcamp: 'Bandcamp', SoundCloud: 'SoundCloud', Tidal: 'Tidal', AmazonMusic: 'Amazon Music',
}

export interface StreamingLink {
  platform: StreamingPlatform
  url: string
}

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
  /** #38 : le morceau est paru sur cette sortie (il a quitté la liste des morceaux) */
  isReleasedHere: boolean
}

export interface Release extends Omit<ReleaseSummary, 'trackCount'> {
  createdAt: string
  updatedAt: string
  tracks: ReleaseTrack[]
  /** #38 : label ou distributeur, code-barres UPC/EAN, liens d'écoute */
  label: string | null
  upc: string | null
  streamingLinks: StreamingLink[]
}

/** #38 : PUT …/releases/{id}/distribution ; remplace les trois. */
export interface ReleaseDistributionInput {
  label: string | null
  upc: string | null
  streamingLinks: string[]
}

/** #38 : ce qu'on garde d'un morceau qui sort. */
export type KeepVersions = 'All' | 'LatestOnly'

/** #38 : POST …/songs/{id}/release. releaseId null : un single au titre du morceau est créé. */
export interface SongReleaseInput extends ReleaseDistributionInput {
  versionId: string
  releaseId: string | null
  releaseDate: string | null
  keep: KeepVersions
  /** Owner seulement : effacer tout de suite au lieu de la corbeille */
  purgeNow: boolean
}

export interface KeptVersion {
  number: number
  releases: string[]
  setlists: string[]
}

export interface SongReleaseResult {
  releaseId: string
  removedVersions: number[]
  keptVersions: KeptVersion[]
  purged: boolean
}

export interface ReleaseInput {
  title: string
  type: ReleaseType
  releaseDate: string | null
}

export type ReleaseExportStatus = 'Pending' | 'Ready' | 'Failed'

/** #24 : archive ZIP de la sortie pour le distributeur (GET …/releases/{id}/exports/latest). */
export interface ReleaseExport {
  id: string
  status: ReleaseExportStatus
  requestedAt: string
  completedAt: string | null
  expiresAt: string | null
  sizeBytes: number | null
}


/** Une piste telle que l'API la reçoit : la place vient de l'ordre dans la liste. */
export interface TrackChoice {
  songId: string
  versionId: string
}