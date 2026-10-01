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
