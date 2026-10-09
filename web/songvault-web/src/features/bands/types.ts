export type BandRole = 'Owner' | 'Member' | 'Guest'

export interface Band {
  id: string
  name: string
  role: BandRole
}