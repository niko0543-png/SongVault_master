import type { BandRole } from '@/features/bands/types'

/** Rôles proposés à l'invitation : inviter un Owner est refusé par l'API (422). */
export type InvitableRole = Exclude<BandRole, 'Owner'>

/** Invitation en attente, vue par l'Owner. */
export interface Invitation {
  id: string
  email: string
  role: InvitableRole
  createdAt: string
  expiresAt: string
}

/** Réponse de création : le lien n'est donné qu'ici, une seule fois. */
export interface InvitationCreated {
  id: string
  email: string
  role: InvitableRole
  expiresAt: string
  link: string
  emailSent: boolean
}

/** Aperçu de /invite/<jeton>, visible sans compte. */
export interface InvitationPreview {
  bandName: string
  email: string
  role: InvitableRole
  expiresAt: string
}