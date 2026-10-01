import type { SongFile } from '@/features/files/types'
import { filesApi } from '@/features/files/api/filesApi'

export interface Track {
  id: string
  title: string
  subtitle: string
  src: string
}

export function toTrack(args: {
  songId: string
  songTitle: string
  versionId: string
  versionNumber: number
  file: SongFile
}): Track {
  return {
    id: args.file.id,
    title: `${args.songTitle} — v${args.versionNumber}`,
    subtitle: args.file.originalFileName,
    src: filesApi.contentUrl(args.songId, args.versionId, args.file.id),
  }
}
