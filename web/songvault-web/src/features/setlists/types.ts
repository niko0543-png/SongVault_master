// ---- Limites : les MÊMES que le domaine C# ----
export const SETLIST_NAME_MAX = 200
export const SETLIST_VENUE_MAX = 200
export const SETLIST_MAX_ITEMS = 50
export const SETLIST_NOTES_MAX = 500

// ---- Contrats API ----
export interface SetlistSummary {
  id: string
  name: string
  /** "2026-11-28", ou null pour une setlist de travail */
  date: string | null
  venue: string | null
  itemCount: number
}

export interface SetlistVersionOption {
  id: string
  number: number
  title: string
}

export interface SetlistItem {
  position: number
  songId: string
  songTitle: string
  /** Version choisie ; null = la dernière du morceau */
  versionId: string | null
  /** Version qui sera jouée (choisie, sinon la dernière) : tonalité, BPM et durée audio viennent d'elle */
  playedVersionId: string | null
  versions: SetlistVersionOption[]
  key: string | null
  bpm: number | null
  notes: string | null
  /** Durée saisie à la main (ms) ; prime sur la durée audio */
  manualDurationMs: number | null
  /** Durée du dernier fichier audio analysé de la version jouée (ms) */
  audioDurationMs: number | null
  /** Durée retenue : saisie, sinon audio, sinon null */
  durationMs: number | null
}

export interface Setlist extends Omit<SetlistSummary, 'itemCount'> {
  createdAt: string
  updatedAt: string
  items: SetlistItem[]
  /** Somme exacte des durées connues, calculée par l'API (ms) */
  totalDurationMs: number
  /** Lignes sans durée, que le total ne compte pas */
  missingDurationCount: number
}

export interface SetlistInput {
  name: string
  date: string | null
  venue: string | null
}

/** Une ligne telle que l'API la reçoit : la place vient de l'ordre dans la liste. */
export interface SetlistItemChoice {
  songId: string
  versionId: string | null
  notes: string | null
  durationMs: number | null
}