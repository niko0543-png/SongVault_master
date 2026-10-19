import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import type { TrashItem, TrashItemKind } from '../types'

const path = (item: { kind: TrashItemKind; id: string }) => bandPath(`/trash/${item.kind.toLowerCase()}/${item.id}`)

export const trashApi = {
  /** La corbeille du groupe actif, le plus récent d'abord. */
  list: () => request<TrashItem[]>(bandPath('/trash')),
  /** Remet l'élément en place ; 409 si ce qui le contient est aussi à la corbeille. */
  restore: (item: { kind: TrashItemKind; id: string }) => request<void>(`${path(item)}/restore`, { method: 'POST' }),
  /** Supprime définitivement, sans attendre les 30 jours (Owner). */
  remove: (item: { kind: TrashItemKind; id: string }) => request<void>(path(item), { method: 'DELETE' }),
}