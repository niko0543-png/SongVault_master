/** #23 : une piste du mélangeur, telle que l'API la rend (ADR 0030). */
export interface Stem {
  fileId: string
  name: string
  position: number
  originalFileName: string
  /** Durée de l'aperçu MP3 prêt ; null tant qu'il se prépare. */
  durationMs: number | null
  /** Fichier sain et aperçu prêt : le mélangeur peut le charger. */
  playable: boolean
}

export interface StemInput {
  fileId: string
  name: string
}

/** Mêmes limites que le serveur (SongFile.StemNameMaxLength, SongVersion.MaxStems). */
export const STEM_NAME_MAX = 60
export const STEMS_MAX = 8