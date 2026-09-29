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
