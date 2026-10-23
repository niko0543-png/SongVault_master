import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import type { Setlist, SetlistInput, SetlistItemChoice, SetlistSummary } from '../types'

const path = (suffix = '') => bandPath(`/setlists${suffix}`)

export const setlistsApi = {
  list: () => request<SetlistSummary[]>(path()),
  get: (id: string) => request<Setlist>(path(`/${id}`)),
  create: (input: SetlistInput) => request<Setlist>(path(), { method: 'POST', body: input }),
  update: (id: string, input: SetlistInput) => request<void>(path(`/${id}`), { method: 'PUT', body: input }),
  remove: (id: string) => request<void>(path(`/${id}`), { method: 'DELETE' }),
  /** Remplace toutes les lignes par cette liste, dans cet ordre ; renvoie la setlist à jour, total compris. */
  setItems: (id: string, items: SetlistItemChoice[]) =>
    request<Setlist>(path(`/${id}/items`), { method: 'PUT', body: { items } }),
}

/** Lignes actuelles d'une setlist, au format attendu par setItems. */
export function toChoices(setlist: Pick<Setlist, 'items'>): SetlistItemChoice[] {
  return setlist.items.map((i) => ({
    songId: i.songId, versionId: i.versionId, notes: i.notes, durationMs: i.manualDurationMs, commentBefore: i.commentBefore,
  }))
}