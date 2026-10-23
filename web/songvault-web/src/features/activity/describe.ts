import type { RouteLocationRaw } from 'vue-router'
import { t, type MessageKey, type Params } from '@/shared/i18n'
import type { ActivityItem } from './types'

/** Un morceau de phrase ; avec to, c'est un lien. */
export interface SentencePart {
  text: string
  to?: RouteLocationRaw
}

/** Qui : « Vous », l'adresse de l'auteur, ou « Compte supprimé ». */
export function actorLabel(item: ActivityItem, myId: string | null): string {
  if (item.actorId !== null && item.actorId === myId) return t('activity.describe.you')
  return item.actorEmail ?? t('activity.describe.deletedAccount')
}

/**
 * « Vous avez ajouté la v4 de Nocturne », « ana@… a commenté prise.wav (v2 de Nocturne) ».
 * Le morceau ou la version est un lien, sauf si le morceau a été supprimé depuis.
 */
export function describe(item: ActivityItem, myId: string | null): SentencePart[] {
  const actor = actorLabel(item, myId)
  const mine = item.actorId !== null && item.actorId === myId
  const song: SentencePart = { text: item.songTitle, to: item.songExists ? { name: 'song-detail', params: { songId: item.songId } } : undefined }
  const version: SentencePart = {
    text: t('activity.describe.version', { number: item.versionNumber ?? '?', title: item.songTitle }),
    to: item.songExists && item.versionId
      ? { name: 'version-detail', params: { songId: item.songId, versionId: item.versionId } }
      : undefined,
  }
  const file = item.fileName ?? t('activity.describe.aFile')
  // « Vous avez » / « ana@… a » : la phrase entière est dans le catalogue, l'action aussi
  const start = (action: MessageKey, params?: Params): SentencePart => ({
    text: `${t(mine ? 'activity.describe.youDid' : 'activity.describe.theyDid', { actor, action: t(action, params) })} `,
  })

  switch (item.type) {
    case 'SongCreated': return [start('activity.describe.actions.songCreated'), song]
    case 'SongUpdated': return [start('activity.describe.actions.songUpdated'), song]
    case 'SongDeleted': return [start('activity.describe.actions.songDeleted'), { text: item.songTitle }]
    case 'VersionAdded': return [start('activity.describe.actions.versionAdded'), version]
    case 'VersionUpdated': return [start('activity.describe.actions.versionUpdated'), version]
    case 'FileAdded': return [start('activity.describe.actions.fileAdded', { file }), version]
    case 'FileRemoved': return [start('activity.describe.actions.fileRemoved', { file }), version]
    case 'CommentAdded': return [start('activity.describe.actions.commentAdded', { file }), version]
    case 'CommentReplied': return [start('activity.describe.actions.commentReplied', { file }), version]
    default: {
      const unreachable: never = item.type
      return [{ text: `${actor} : ${String(unreachable)}` }]
    }
  }
}