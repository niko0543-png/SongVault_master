export interface Song {
  id: string
  title: string
  artist: string | null
  description: string | null
  createdAt: string // ISO 8601 : le JSON n'a pas de type date
  updatedAt: string
}

/** Données envoyées par POST et PUT (le serveur attribue id et dates). */
export interface SongInput {
  title: string
  artist: string | null
  description: string | null
}
