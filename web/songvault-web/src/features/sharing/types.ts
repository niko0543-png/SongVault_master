import type { ReleaseType } from '@/features/releases/types'

export type ShareTargetType = 'Version' | 'Release'
export type ShareLinkStatus = 'Active' | 'Expired' | 'Revoked'
export type ShareAccessKind = 'Opened' | 'Played' | 'WrongPassword'

/** Ce que l'on partage : une version (id de la version) ou une sortie (id de la sortie). */
export interface ShareTarget {
  type: ShareTargetType
  id: string
}

/** Un lien, vu des membres. Le lien complet n'est jamais relu : seule la création le donne. */
export interface ShareLink {
  id: string
  targetType: ShareTargetType
  targetId: string
  label: string | null
  hasPassword: boolean
  status: ShareLinkStatus
  createdAt: string
  expiresAt: string
  revokedAt: string | null
  createdByEmail: string | null
  openCount: number
  lastOpenedAt: string | null
}

export interface ShareLinkCreated {
  link: ShareLink
  url: string
}

export interface ShareLinkInput {
  label: string | null
  expiresInDays: number
  password: string | null
}

/** Une ligne du journal. visitor : même valeur pour un même visiteur sur ce lien, sans son adresse IP. */
export interface ShareLinkAccess {
  at: string
  kind: ShareAccessKind
  fileName: string | null
  visitor: string | null
  userAgent: string | null
}

// ---- Page publique ----
export interface SharedFile {
  id: string
  fileName: string
  contentType: string
  sizeBytes: number
}

export interface SharedTrack {
  number: number
  songTitle: string
  artist: string | null
  versionNumber: number
  versionTitle: string
  files: SharedFile[]
}

export interface SharedContent {
  targetType: ShareTargetType
  bandName: string
  title: string
  releaseType: ReleaseType | null
  releaseDate: string | null
  hasCover: boolean
  expiresAt: string
  tracks: SharedTrack[]
}

// ---- Limites : les MÊMES que le domaine C# (ShareLink) ----
export const SHARE_LABEL_MAX = 100
export const SHARE_PASSWORD_MIN = 6
export const SHARE_PASSWORD_MAX = 128
export const SHARE_DURATIONS = [1, 7, 30, 90] as const
export const SHARE_DEFAULT_DURATION = 30

export const shareStatusLabels: Record<ShareLinkStatus, string> = {
  Active: 'Actif',
  Expired: 'Expiré',
  Revoked: 'Révoqué',
}

export const accessKindLabels: Record<ShareAccessKind, string> = {
  Opened: 'Page ouverte',
  Played: 'Écoute',
  WrongPassword: 'Mauvais mot de passe',
}