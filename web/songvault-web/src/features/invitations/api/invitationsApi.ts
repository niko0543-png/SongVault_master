import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import type { Band } from '@/features/bands/types'
import type { Invitation, InvitationCreated, InvitationPreview, InvitableRole } from '../types'

const byToken = (token: string) => `/invitations/${encodeURIComponent(token)}`

export const invitationsApi = {
  // Groupe actif, Owner uniquement
  listPending: () => request<Invitation[]>(bandPath('/invitations')),
  create: (email: string, role: InvitableRole) =>
    request<InvitationCreated>(bandPath('/invitations'), { method: 'POST', body: { email, role } }),
  revoke: (id: string) => request<void>(bandPath(`/invitations/${id}`), { method: 'DELETE' }),

  // Lien reçu par e-mail
  preview: (token: string) => request<InvitationPreview>(byToken(token)),
  accept: (token: string) => request<Band>(`${byToken(token)}/accept`, { method: 'POST' }),
}