export type SongFileType = 'Audio' | 'Tablature' | 'Document'

/** #15 : état de l'aperçu MP3 et de la forme d'onde d'un fichier audio (null pour les autres). */
export type AudioPreviewStatus = 'Pending' | 'Ready' | 'Failed'

/**
 * #17 : analyse antivirus. Un fichier Clean, ou NotScanned (antivirus désactivé avant la commercialisation),
 * s'écoute et se télécharge.
 */
export type FileScanStatus = 'Pending' | 'Clean' | 'Infected' | 'Failed' | 'NotScanned'

export interface SongFile {
  id: string
  originalFileName: string
  contentType: string
  sizeBytes: number
  fileType: SongFileType
  uploadedAt: string
  /** Absent des réponses d'avant #15 et des faux des tests : traité comme null. */
  preview?: AudioPreviewStatus | null
  /** Absent des faux des tests d'avant #17 : traité comme Clean (l'API, elle, refuse tout fichier non déclaré sain). */
  scan?: FileScanStatus
  /** Fichier Infected : nom de la menace trouvée. */
  scanThreat?: string | null
}

/** #17 : le fichier peut être écouté et téléchargé. */
export function isReadable(file: SongFile): boolean {
  const scan = file.scan ?? 'Clean'
  return scan === 'Clean' || scan === 'NotScanned'
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
/** Découpage d'un enregistrement (ADR 0050). */
export type AudioSplitStatus = 'Pending' | 'Done' | 'Failed'

export interface AudioSplitPart {
  startMs: number
  endMs: number
  title: string
  songId: string | null
  /** Une fois fait : le morceau et la version qui ont reçu le passage. */
  createdSongId: string | null
  createdVersionId: string | null
}

export interface AudioSplit {
  id: string
  status: AudioSplitStatus
  error: string | null
  requestedAt: string
  completedAt: string | null
  parts: AudioSplitPart[]
}
