import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import type { ShareLink, ShareLinkAccess, ShareLinkCreated, ShareLinkInput, SharedContent, ShareTarget } from '../types'

const byToken = (token: string) => `/share/${encodeURIComponent(token)}`
const targetQuery = (target: ShareTarget) =>
  `${target.type === 'Version' ? 'versionId' : 'releaseId'}=${encodeURIComponent(target.id)}`

export const sharingApi = {
  // Groupe actif, Member ou Owner
  list: (target: ShareTarget) => request<ShareLink[]>(bandPath(`/share-links?${targetQuery(target)}`)),
  create: (target: ShareTarget, input: ShareLinkInput) =>
    request<ShareLinkCreated>(bandPath('/share-links'), {
      method: 'POST',
      body: { targetType: target.type, targetId: target.id, ...input },
    }),
  revoke: (id: string) => request<void>(bandPath(`/share-links/${id}/revoke`), { method: 'POST' }),
  remove: (id: string) => request<void>(bandPath(`/share-links/${id}`), { method: 'DELETE' }),
  accesses: (id: string) => request<ShareLinkAccess[]>(bandPath(`/share-links/${id}/accesses`)),

  // Page publique /s/<jeton>, sans compte. 403 : mot de passe attendu ; 404 : lien inconnu, expiré ou révoqué.
  open: (token: string) => request<SharedContent>(byToken(token)),
  unlock: (token: string, password: string) =>
    request<void>(`${byToken(token)}/unlock`, { method: 'POST', body: { password } }),
  // Adresses pour <audio> et <img> : le cookie de déverrouillage suit, il est posé sur /api/share/<jeton>
  fileUrl: (token: string, fileId: string) => `/api${byToken(token)}/files/${fileId}/content`,
  coverUrl: (token: string) => `/api${byToken(token)}/cover`,
}