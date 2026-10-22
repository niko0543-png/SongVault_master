/** #29 : un point de synchronisation, comme le FlatSyncPoint d'alphaTab (ADR 0036). */
export interface TabSyncPoint {
  /** Mesure, à partir de 0. */
  barIndex: number
  /** Passage dans la mesure quand elle est reprise (0 = la première fois). */
  barOccurence: number
  /** Place dans la mesure, de 0 (son début) à 1 (sa fin). */
  barPosition: number
  /** Instant de la maquette, en millisecondes. */
  millisecondOffset: number
}

/** Le calage d'une tablature : la maquette suivie (un fichier audio de la même version) et les points. */
export interface TabSync {
  audioFileId: string
  audioFileName: string
  points: TabSyncPoint[]
}

/** Une maquette que l'on peut choisir : un fichier audio lisible de la version. */
export interface SyncAudio {
  id: string
  name: string
}

/** Ce que FileList donne au lecteur pour lire et enregistrer le calage d'une tablature. */
export interface SyncContext {
  songId: string
  versionId: string
  fileId: string
  audios: SyncAudio[]
  /** Member ou Owner : peut caler et supprimer le calage. */
  canEdit: boolean
}

/** Une mesure telle qu'elle est jouée : une mesure reprise apparaît deux fois, avec son numéro de passage. */
export interface PlayedBar {
  barIndex: number
  occurrence: number
}