export type SongFileType = 'Audio' | 'Tablature' | 'Document'

export interface SongFile {
  id: string
  originalFileName: string
  contentType: string
  sizeBytes: number
  fileType: SongFileType
  uploadedAt: string
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