import type { RouteLocationRaw } from 'vue-router'
import type { ActivityItem } from './types'

/** Un morceau de phrase ; avec to, c'est un lien. */
export interface SentencePart {
  text: string
  to?: RouteLocationRaw
}

/** Qui : « Vous », l'adresse de l'auteur, ou « Compte supprimé ». */
export function actorLabel(item: ActivityItem, myId: string | null): string {
  if (item.actorId !== null && item.actorId === myId) return 'Vous'
  return item.actorEmail ?? 'Compte supprimé'
}

/**
 * « Vous avez ajouté la v4 de Nocturne », « ana@… a commenté prise.wav (v2 de Nocturne) ».
 * Le morceau ou la version est un lien, sauf si le morceau a été supprimé depuis.
 */
export function describe(item: ActivityItem, myId: string | null): SentencePart[] {
  const actor = actorLabel(item, myId)
  const has = actor === 'Vous' ? 'avez' : 'a'
  const song: SentencePart = { text: item.songTitle, to: item.songExists ? { name: 'song-detail', params: { songId: item.songId } } : undefined }
  const version: SentencePart = {
    text: `v${item.versionNumber ?? '?'} de ${item.songTitle}`,
    to: item.songExists && item.versionId
      ? { name: 'version-detail', params: { songId: item.songId, versionId: item.versionId } }
      : undefined,
  }
  const file = item.fileName ?? 'un fichier'
  const start = (verb: string): SentencePart => ({ text: `${actor} ${has} ${verb} ` })

  switch (item.type) {
    case 'SongCreated': return [start('créé le morceau'), song]
    case 'SongUpdated': return [start('modifié le morceau'), song]
    case 'SongDeleted': return [start('supprimé le morceau'), { text: item.songTitle }]
    case 'VersionAdded': return [start('ajouté la'), version]
    case 'VersionUpdated': return [start('modifié la'), version]
    case 'FileAdded': return [start(`ajouté ${file} à la`), version]
    case 'FileRemoved': return [start(`retiré ${file} de la`), version]
    case 'CommentAdded': return [start(`commenté ${file} dans la`), version]
    case 'CommentReplied': return [start(`répondu à un commentaire sur ${file} dans la`), version]
    default: {
      const unreachable: never = item.type
      return [{ text: `${actor} : ${String(unreachable)}` }]
    }
  }
}