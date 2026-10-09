export type BandRole = 'Owner' | 'Member' | 'Guest'

export interface Band {
  id: string
  name: string
  role: BandRole
}

export interface BandMember {
  userId: string
  email: string
  role: BandRole
  joinedAt: string
}