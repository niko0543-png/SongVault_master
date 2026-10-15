export type SongFileType = 'Audio' | 'Tablature' | 'Document'

/** #15 : état de l'aperçu MP3 et de la forme d'onde d'un fichier audio (null pour les autres). */
export type AudioPreviewStatus = 'Pending' | 'Ready' | 'Failed'

export interface SongFile {
  id: string
  originalFileName: string
  contentType: string
  sizeBytes: number
  fileType: SongFileType
  uploadedAt: string
  /** Absent des réponses d'avant #15 et des faux des tests : traité comme null. */
  preview?: AudioPreviewStatus | null
}

/** #15 : 1000 crêtes de 0 à 255, la plus forte valant 255. */
export interface AudioWaveform {
  durationMs: number
  peaks: number[]
}

export interface FilePolicy {
  allowedExtensions: string[]
  maxSizeBytes: number
}

/** Réponse à l'annonce d'un envoi direct (ADR 0012). */
export interface FileUploadTicket {
  uploadId: string
  /** URL signée du stockage, ou route de l'API en stockage local. */
  uploadUrl: string
  /** En-têtes à joindre au PUT (x-ms-blob-type pour Azure). */
  headers: Record<string, string>
  expiresAt: string
}